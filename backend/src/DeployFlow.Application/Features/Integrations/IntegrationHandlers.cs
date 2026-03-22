using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using System.Net.Http.Json;
using System.Text.Json;

namespace DeployFlow.Application.Features.Integrations;

// ─── Shared Constants ─────────────────────────────────────────────────────────

internal static class IntegrationChannels
{
    public const string DockerHub = "docker_hub";
    public const string GitLab    = "gitlab";
    public const string Slack     = "slack";
    public const string AWS       = "aws";
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record IntegrationStatusDto(string Name, bool IsConnected, string? Username, DateTime? ConnectedAt);

// ─── Get Docker Hub Status ────────────────────────────────────────────────────

public record GetDockerHubStatusQuery : IRequest<Result<IntegrationStatusDto>>;

public class GetDockerHubStatusQueryHandler : IRequestHandler<GetDockerHubStatusQuery, Result<IntegrationStatusDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetDockerHubStatusQueryHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<IntegrationStatusDto>> Handle(GetDockerHubStatusQuery request, CancellationToken ct)
    {
        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var cfg = configs.FirstOrDefault(c => c.Channel == IntegrationChannels.DockerHub);

        if (cfg is null)
            return Result<IntegrationStatusDto>.Success(new IntegrationStatusDto("Docker Hub", false, null, null));

        string? username = null;
        try
        {
            var doc = JsonDocument.Parse(cfg.ConfigJson);
            doc.RootElement.TryGetProperty("username", out var u);
            username = u.GetString();
        }
        catch { /* malformed JSON — treat as not connected */ }

        return Result<IntegrationStatusDto>.Success(
            new IntegrationStatusDto("Docker Hub", cfg.IsEnabled, username, cfg.UpdatedAt));
    }
}

// ─── Connect Docker Hub ───────────────────────────────────────────────────────

public record ConnectDockerHubCommand(string Username, string AccessToken) : IRequest<Result<IntegrationStatusDto>>;

public class ConnectDockerHubCommandValidator : AbstractValidator<ConnectDockerHubCommand>
{
    public ConnectDockerHubCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AccessToken).NotEmpty().MinimumLength(6);
    }
}

public class ConnectDockerHubCommandHandler : IRequestHandler<ConnectDockerHubCommand, Result<IntegrationStatusDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IEncryptionService _encryption;

    public ConnectDockerHubCommandHandler(IUnitOfWork uow, ICurrentUser cu, IEncryptionService enc)
    { _uow = uow; _currentUser = cu; _encryption = enc; }

    public async Task<Result<IntegrationStatusDto>> Handle(ConnectDockerHubCommand request, CancellationToken ct)
    {
        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var existing = configs.FirstOrDefault(c => c.Channel == IntegrationChannels.DockerHub);

        var configJson = JsonSerializer.Serialize(new
        {
            username    = request.Username,
            accessToken = _encryption.Encrypt(request.AccessToken),
        });

        if (existing is not null)
        {
            existing.ConfigJson  = configJson;
            existing.IsEnabled   = true;
            existing.UpdatedAt   = DateTime.UtcNow;
            await _uow.NotificationConfigs.UpdateAsync(existing, ct);
        }
        else
        {
            var cfg = new NotificationConfig
            {
                TenantId   = _currentUser.TenantId,
                Channel    = IntegrationChannels.DockerHub,
                Name       = "Docker Hub",
                IsEnabled  = true,
                ConfigJson = configJson,
                EventsJson = "[]",
            };
            await _uow.NotificationConfigs.AddAsync(cfg, ct);
        }

        await _uow.SaveChangesAsync(ct);

        return Result<IntegrationStatusDto>.Success(
            new IntegrationStatusDto("Docker Hub", true, request.Username, DateTime.UtcNow));
    }
}

// ─── Disconnect Docker Hub ────────────────────────────────────────────────────

public record DisconnectDockerHubCommand : IRequest<Result>;

public class DisconnectDockerHubCommandHandler : IRequestHandler<DisconnectDockerHubCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DisconnectDockerHubCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(DisconnectDockerHubCommand request, CancellationToken ct)
    {
        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var cfg = configs.FirstOrDefault(c => c.Channel == IntegrationChannels.DockerHub);

        if (cfg is null)
            return Result.Failure("Docker Hub is not connected.", 404);

        await _uow.NotificationConfigs.DeleteAsync(cfg, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Verify Docker Hub Credentials (without saving) ──────────────────────────

public record VerifyDockerHubCommand(string Username, string AccessToken) : IRequest<Result<IntegrationStatusDto>>;

public class VerifyDockerHubCommandHandler : IRequestHandler<VerifyDockerHubCommand, Result<IntegrationStatusDto>>
{
    private readonly IHttpClientFactory _http;

    public VerifyDockerHubCommandHandler(IHttpClientFactory http)
    { _http = http; }

    public async Task<Result<IntegrationStatusDto>> Handle(VerifyDockerHubCommand request, CancellationToken ct)
    {
        try
        {
            var client = _http.CreateClient("notifications");
            var loginPayload = new { username = request.Username, password = request.AccessToken };
            var response = await client.PostAsJsonAsync("https://hub.docker.com/v2/users/login/", loginPayload, ct);

            if (!response.IsSuccessStatusCode)
                return Result<IntegrationStatusDto>.Failure("Invalid Docker Hub credentials. Please check your username and access token.", 400);

            return Result<IntegrationStatusDto>.Success(
                new IntegrationStatusDto("Docker Hub", true, request.Username, DateTime.UtcNow));
        }
        catch (Exception)
        {
            return Result<IntegrationStatusDto>.Failure("Could not reach Docker Hub. Please check your network connection.", 503);
        }
    }
}

// ─── Connect Slack ────────────────────────────────────────────────────────────

public record ConnectSlackCommand(string WebhookUrl) : IRequest<Result<IntegrationStatusDto>>;

public class ConnectSlackCommandHandler : IRequestHandler<ConnectSlackCommand, Result<IntegrationStatusDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpClientFactory _http;

    public ConnectSlackCommandHandler(IUnitOfWork uow, ICurrentUser cu, IHttpClientFactory http)
    { _uow = uow; _currentUser = cu; _http = http; }

    public async Task<Result<IntegrationStatusDto>> Handle(ConnectSlackCommand request, CancellationToken ct)
    {
        // Test the webhook with a verification message
        try
        {
            var client = _http.CreateClient("notifications");
            var testPayload = new { text = "✅ DeployFlow Slack integration connected successfully!" };
            var response = await client.PostAsJsonAsync(request.WebhookUrl, testPayload, ct);
            if (!response.IsSuccessStatusCode)
                return Result<IntegrationStatusDto>.Failure("Slack webhook verification failed. Please check the webhook URL.", 400);
        }
        catch
        {
            return Result<IntegrationStatusDto>.Failure("Could not reach Slack. Please check the webhook URL.", 503);
        }

        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var existing = configs.FirstOrDefault(c => c.Channel == IntegrationChannels.Slack);

        var configJson = JsonSerializer.Serialize(new { webhookUrl = request.WebhookUrl });

        if (existing is not null)
        {
            existing.ConfigJson = configJson;
            existing.IsEnabled  = true;
            existing.UpdatedAt  = DateTime.UtcNow;
            await _uow.NotificationConfigs.UpdateAsync(existing, ct);
        }
        else
        {
            await _uow.NotificationConfigs.AddAsync(new NotificationConfig
            {
                TenantId   = _currentUser.TenantId,
                Channel    = IntegrationChannels.Slack,
                Name       = "Slack",
                IsEnabled  = true,
                ConfigJson = configJson,
                EventsJson = "[\"deployment.succeeded\",\"deployment.failed\",\"alert.triggered\"]",
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<IntegrationStatusDto>.Success(new IntegrationStatusDto("Slack", true, null, DateTime.UtcNow));
    }
}

// ─── Connect AWS ──────────────────────────────────────────────────────────────

public record ConnectAwsCommand(string AccessKeyId, string SecretAccessKey, string Region) : IRequest<Result<IntegrationStatusDto>>;

public class ConnectAwsCommandHandler : IRequestHandler<ConnectAwsCommand, Result<IntegrationStatusDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IEncryptionService _encryption;

    public ConnectAwsCommandHandler(IUnitOfWork uow, ICurrentUser cu, IEncryptionService enc)
    { _uow = uow; _currentUser = cu; _encryption = enc; }

    public async Task<Result<IntegrationStatusDto>> Handle(ConnectAwsCommand request, CancellationToken ct)
    {
        var configJson = JsonSerializer.Serialize(new
        {
            accessKeyId     = request.AccessKeyId,
            secretAccessKey = _encryption.Encrypt(request.SecretAccessKey),
            region          = request.Region,
        });

        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var existing = configs.FirstOrDefault(c => c.Channel == IntegrationChannels.AWS);

        if (existing is not null)
        {
            existing.ConfigJson = configJson;
            existing.IsEnabled  = true;
            existing.UpdatedAt  = DateTime.UtcNow;
            await _uow.NotificationConfigs.UpdateAsync(existing, ct);
        }
        else
        {
            await _uow.NotificationConfigs.AddAsync(new NotificationConfig
            {
                TenantId   = _currentUser.TenantId,
                Channel    = IntegrationChannels.AWS,
                Name       = "AWS",
                IsEnabled  = true,
                ConfigJson = configJson,
                EventsJson = "[]",
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<IntegrationStatusDto>.Success(new IntegrationStatusDto("AWS", true, request.AccessKeyId, DateTime.UtcNow));
    }
}

// ─── Connect Grafana ─────────────────────────────────────────────────────────

public record ConnectGrafanaCommand(string Url, string ApiToken) : IRequest<Result<IntegrationStatusDto>>;

public class ConnectGrafanaCommandHandler : IRequestHandler<ConnectGrafanaCommand, Result<IntegrationStatusDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IEncryptionService _encryption;

    public ConnectGrafanaCommandHandler(IUnitOfWork uow, ICurrentUser cu, IEncryptionService enc)
    { _uow = uow; _currentUser = cu; _encryption = enc; }

    public async Task<Result<IntegrationStatusDto>> Handle(ConnectGrafanaCommand request, CancellationToken ct)
    {
        var configJson = JsonSerializer.Serialize(new
        {
            url      = request.Url.TrimEnd('/'),
            apiToken = _encryption.Encrypt(request.ApiToken),
        });

        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var existing = configs.FirstOrDefault(c => c.Channel == "grafana");

        if (existing is not null)
        {
            existing.ConfigJson = configJson;
            existing.IsEnabled  = true;
            existing.UpdatedAt  = DateTime.UtcNow;
            await _uow.NotificationConfigs.UpdateAsync(existing, ct);
        }
        else
        {
            await _uow.NotificationConfigs.AddAsync(new NotificationConfig
            {
                TenantId   = _currentUser.TenantId,
                Channel    = "grafana",
                Name       = "Grafana",
                IsEnabled  = true,
                ConfigJson = configJson,
                EventsJson = "[]",
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<IntegrationStatusDto>.Success(new IntegrationStatusDto("Grafana", true, null, DateTime.UtcNow));
    }
}

