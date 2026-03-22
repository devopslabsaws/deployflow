using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using System.Text.Json;

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
