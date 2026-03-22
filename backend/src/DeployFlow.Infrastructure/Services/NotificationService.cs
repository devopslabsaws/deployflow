using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace DeployFlow.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _emailService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork uow,
        IEmailService emailService,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationService> logger)
    {
        _uow = uow;
        _emailService = emailService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyDeploymentStarted(Guid tenantId, Guid projectId, Guid deploymentId, string projectName, CancellationToken ct = default)
        => await SendToChannels(tenantId, "deployment.started", new
        {
            projectName,
            deploymentId,
            message = $"🚀 Deployment started for **{projectName}**",
            color = "#4F46E5"
        }, ct);

    public async Task NotifyDeploymentSucceeded(Guid tenantId, Guid projectId, Guid deploymentId, string projectName, string? url = null, CancellationToken ct = default)
        => await SendToChannels(tenantId, "deployment.succeeded", new
        {
            projectName,
            deploymentId,
            url,
            message = $"✅ Deployment succeeded for **{projectName}**" + (url is not null ? $"\n🌐 {url}" : ""),
            color = "#10B981"
        }, ct);

    public async Task NotifyDeploymentFailed(Guid tenantId, Guid projectId, Guid deploymentId, string projectName, string? reason = null, CancellationToken ct = default)
        => await SendToChannels(tenantId, "deployment.failed", new
        {
            projectName,
            deploymentId,
            reason,
            message = $"❌ Deployment failed for **{projectName}**" + (reason is not null ? $"\n{reason}" : ""),
            color = "#EF4444"
        }, ct);

    public async Task NotifyAlertTriggered(Guid tenantId, string alertName, string severity, CancellationToken ct = default)
        => await SendToChannels(tenantId, "alert.triggered", new
        {
            alertName,
            severity,
            message = $"⚠️ Alert triggered: **{alertName}** [{severity.ToUpperInvariant()}]",
            color = "#F59E0B"
        }, ct);

    private async Task SendToChannels(Guid tenantId, string eventType, object payload, CancellationToken ct)
    {
        try
        {
            var configs = await _uow.NotificationConfigs.GetEnabledByTenantAsync(tenantId, ct);
            foreach (var config in configs)
            {
                var events = JsonSerializer.Deserialize<List<string>>(config.EventsJson) ?? new();
                if (!events.Contains(eventType) && !events.Contains("*")) continue;

                await (config.Channel.ToLower() switch
                {
                    "slack" => SendSlackAsync(config.ConfigJson, payload, ct),
                    "webhook" => SendWebhookAsync(config.ConfigJson, eventType, payload, ct),
                    "email" => SendEmailAsync(config.ConfigJson, eventType, payload, ct),
                    _ => Task.CompletedTask
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send {EventType} notification for tenant {TenantId}", eventType, tenantId);
        }
    }

    private async Task SendSlackAsync(string configJson, object payload, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson) ?? new();
        if (!config.TryGetValue("webhookUrl", out var webhookUrl)) return;

        var payloadDict = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(payload));
        var message = payloadDict?.TryGetValue("message", out var msg) == true ? msg?.ToString() : "Notification";

        var slackPayload = new { text = message };
        var client = _httpClientFactory.CreateClient("notifications");
        await client.PostAsJsonAsync(webhookUrl, slackPayload, ct);
    }

    private async Task SendWebhookAsync(string configJson, string eventType, object payload, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson) ?? new();
        if (!config.TryGetValue("url", out var url)) return;

        var client = _httpClientFactory.CreateClient("notifications");
        await client.PostAsJsonAsync(url, new { @event = eventType, data = payload }, ct);
    }

    private async Task SendEmailAsync(string configJson, string eventType, object payload, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson) ?? new();
        if (!config.TryGetValue("to", out var to)) return;

        var payloadDict = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(payload));
        var message = payloadDict?.TryGetValue("message", out var msg) == true ? msg?.ToString() : eventType;

        await _emailService.SendAsync(to, $"DeployFlow: {eventType}", $"<p>{message}</p>", ct);
    }
}
