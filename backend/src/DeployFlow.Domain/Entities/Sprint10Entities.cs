using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

// ─── Outbound Webhook Config (notify external systems on deploy events) ────────

public class OutboundWebhookConfig : TenantEntity
{
    public string Name { get; set; } = default!;
    public string Url { get; set; } = default!;
    /// <summary>JSON array: ["deploy.started","deploy.success","deploy.failed","preview.created"]</summary>
    public string Events { get; set; } = "[\"deploy.success\",\"deploy.failed\"]";
    public string? Secret { get; set; }           // HMAC-SHA256 header X-DeployFlow-Signature
    public bool IsEnabled { get; set; } = true;
    public Guid? ProjectId { get; set; }          // null = global (all projects)
    public int DeliveryCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastDeliveredAt { get; set; }
    public string? LastResponseStatus { get; set; }
}
