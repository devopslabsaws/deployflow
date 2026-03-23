using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using System.Text.Json;
using System.Net.Http.Json;

namespace DeployFlow.Application.Features.Notifications;

// ─── DTO ─────────────────────────────────────────────────────────────────────

public record EmailNotificationConfigDto(bool EmailEnabled, bool DeploymentSuccess, bool DeploymentFailure, string? Email);

// ─── Get Email Notification Config ───────────────────────────────────────────

public record GetEmailNotificationConfigQuery : IRequest<Result<EmailNotificationConfigDto>>;

public class GetEmailNotificationConfigQueryHandler
    : IRequestHandler<GetEmailNotificationConfigQuery, Result<EmailNotificationConfigDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetEmailNotificationConfigQueryHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<EmailNotificationConfigDto>> Handle(
        GetEmailNotificationConfigQuery request, CancellationToken ct)
    {
        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var cfg = configs.FirstOrDefault(c => c.Channel == "email");

        if (cfg is null)
            return Result<EmailNotificationConfigDto>.Success(
                new EmailNotificationConfigDto(true, true, true, _currentUser.Email));

        var events = ParseEvents(cfg.EventsJson);
        return Result<EmailNotificationConfigDto>.Success(new EmailNotificationConfigDto(
            cfg.IsEnabled,
            events.Contains("deployment.succeeded"),
            events.Contains("deployment.failed"),
            ParseEmail(cfg.ConfigJson) ?? _currentUser.Email));
    }

    private static List<string> ParseEvents(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static string? ParseEmail(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("to", out var p) ? p.GetString() : null;
        }
        catch { return null; }
    }
}

// ─── Upsert Email Notification Config ────────────────────────────────────────

public record UpsertEmailNotificationCommand(bool EmailEnabled, bool DeploymentSuccess, bool DeploymentFailure)
    : IRequest<Result>;

public class UpsertEmailNotificationCommandHandler
    : IRequestHandler<UpsertEmailNotificationCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpsertEmailNotificationCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(UpsertEmailNotificationCommand request, CancellationToken ct)
    {
        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var existing = configs.FirstOrDefault(c => c.Channel == "email");

        var events = new List<string>();
        if (request.DeploymentSuccess) events.Add("deployment.succeeded");
        if (request.DeploymentFailure) events.Add("deployment.failed");

        var configJson = JsonSerializer.Serialize(new { to = _currentUser.Email });
        var eventsJson = JsonSerializer.Serialize(events);

        if (existing is not null)
        {
            existing.IsEnabled  = request.EmailEnabled;
            existing.ConfigJson = configJson;
            existing.EventsJson = eventsJson;
            existing.UpdatedAt  = DateTime.UtcNow;
            await _uow.NotificationConfigs.UpdateAsync(existing, ct);
        }
        else
        {
            var cfg = new NotificationConfig
            {
                TenantId   = _currentUser.TenantId,
                Channel    = "email",
                Name       = "Email Notifications",
                IsEnabled  = request.EmailEnabled,
                ConfigJson = configJson,
                EventsJson = eventsJson,
            };
            await _uow.NotificationConfigs.AddAsync(cfg, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record NotificationChannelTestResultDto(
    string Channel,
    bool Success,
    string Message,
    DateTime TestedAt
);

public record TestNotificationChannelCommand(string Channel)
    : IRequest<Result<NotificationChannelTestResultDto>>;

public class TestNotificationChannelCommandHandler
    : IRequestHandler<TestNotificationChannelCommand, Result<NotificationChannelTestResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpClientFactory _http;
    private readonly IEmailService _emailService;

    public TestNotificationChannelCommandHandler(
        IUnitOfWork uow,
        ICurrentUser cu,
        IHttpClientFactory http,
        IEmailService emailService)
    {
        _uow = uow;
        _currentUser = cu;
        _http = http;
        _emailService = emailService;
    }

    public async Task<Result<NotificationChannelTestResultDto>> Handle(TestNotificationChannelCommand request, CancellationToken ct)
    {
        var channel = request.Channel.Trim().ToLowerInvariant();
        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var cfg = configs.FirstOrDefault(c => c.Channel.ToLower() == channel);
        if (cfg is null)
            return Result<NotificationChannelTestResultDto>.Failure("Channel configuration not found.", 404);

        if (!cfg.IsEnabled)
            return Result<NotificationChannelTestResultDto>.Failure("Channel is disabled.", 400);

        var ok = channel switch
        {
            "email" => await SendEmailProbeAsync(cfg.ConfigJson, ct),
            "slack" => await SendWebhookProbeAsync(GetUrl(cfg.ConfigJson, "webhookUrl"), channel, ct),
            "msteams" => await SendWebhookProbeAsync(GetUrl(cfg.ConfigJson, "webhookUrl", "url"), channel, ct),
            "webhook" => await SendWebhookProbeAsync(GetUrl(cfg.ConfigJson, "url", "webhookUrl"), channel, ct),
            "github" => await SendWebhookProbeAsync(GetUrl(cfg.ConfigJson, "url", "webhookUrl"), channel, ct),
            "gitlab" => await SendWebhookProbeAsync(GetUrl(cfg.ConfigJson, "url", "webhookUrl"), channel, ct),
            "cloudflare" => await SendWebhookProbeAsync(GetUrl(cfg.ConfigJson, "url", "webhookUrl"), channel, ct),
            _ => false
        };

        if (!ok)
            return Result<NotificationChannelTestResultDto>.Failure("Channel test failed. Verify credentials and destination URL.", 400);

        return Result<NotificationChannelTestResultDto>.Success(new NotificationChannelTestResultDto(
            channel,
            true,
            "Test notification sent successfully.",
            DateTime.UtcNow));
    }

    private async Task<bool> SendEmailProbeAsync(string configJson, CancellationToken ct)
    {
        var to = ParseString(configJson, "to") ?? _currentUser.Email;
        if (string.IsNullOrWhiteSpace(to)) return false;
        try
        {
            await _emailService.SendAsync(to, "DeployFlow notification test", "<p>Test notification sent successfully.</p>", ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> SendWebhookProbeAsync(string? url, string channel, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            var client = _http.CreateClient("notifications");
            var response = await client.PostAsJsonAsync(url, new
            {
                eventType = "notification.test",
                channel,
                message = "DeployFlow notification channel test"
            }, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetUrl(string configJson, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = ParseString(configJson, key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static string? ParseString(string json, string key)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(key, out var p) ? p.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
