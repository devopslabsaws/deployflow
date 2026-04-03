using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

// ─── Server Metrics ───────────────────────────────────────────────────────────

public class ServerMetrics : BaseEntity
{
    public Guid ServerId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageBytes { get; set; }
    public long MemoryTotalBytes { get; set; }
    public long DiskUsageBytes { get; set; }
    public long DiskTotalBytes { get; set; }
    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }
    public int ActiveContainers { get; set; }
    public double LoadAverage1m { get; set; }
}

// ─── Cost Record ──────────────────────────────────────────────────────────────

public class CostRecord : TenantEntity
{
    // ResourceType holds the billing category: "compute", "memory", "storage", "database", "container"
    public string ResourceType { get; set; } = default!;
    public string ResourceName { get; set; } = default!;
    public Guid? ResourceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Period { get; set; } = default!;   // "2024-01"
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

// ─── Volume ───────────────────────────────────────────────────────────────────

public class Volume : TenantEntity
{
    public string Name { get; set; } = default!;
    public Guid? ServerId { get; set; }
    public string? MountPath { get; set; }
    public long SizeBytes { get; set; }
    public string Driver { get; set; } = "local";
    public VolumeStatus Status { get; set; } = VolumeStatus.Active;
    public string? DockerName { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public enum VolumeStatus { Active, Inactive, Error }

// ─── Project Environment (dev/staging/production) ─────────────────────────────

public class ProjectEnvironment : TenantEntity
{
    public Guid? ProjectId { get; set; }
    public string Name { get; set; } = default!;   // "production"
    public string Slug { get; set; } = default!;   // "production"
    public string? Branch { get; set; }
    public bool AutoDeployOnPush { get; set; } = true;
    public bool RequiresApproval { get; set; }
    public bool IsDefault { get; set; }
    public bool IsProduction { get; set; }
    public int Order { get; set; }
}

// ─── Project Deployment Environment Mapping ─────────────────────────────────

public class ProjectDeploymentEnvironment : TenantEntity
{
    public Guid ProjectId { get; set; }
    public string EnvironmentName { get; set; } = default!; // Dev, QA, Prod
    public string Branch { get; set; } = default!;          // dev, staging, main
    public bool AutoDeploy { get; set; } = true;
    public bool RequiresApproval { get; set; }
    public bool IsDefault { get; set; }
    public int Order { get; set; }
}

// ─── Project Member (project-scoped RBAC) ─────────────────────────────────────

public class ProjectMember : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "developer"; // viewer | developer | admin
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

// ─── Container (runtime, live view) ──────────────────────────────────────────

public class Container : TenantEntity
{
    public string ContainerId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Image { get; set; } = default!;
    public string Status { get; set; } = default!;
    public Guid ServerId { get; set; }
    public Guid? ServiceId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime StartedAt { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryBytes { get; set; }
    public long MemoryLimitBytes { get; set; }
    public string Ports { get; set; } = "[]";
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}

// ─── ProjectTag (proper class for EF) ────────────────────────────────────────

public class ProjectTag : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = default!;
}

// ─── ApplicationRole (Identity) ──────────────────────────────────────────────

public class ApplicationRole : Microsoft.AspNetCore.Identity.IdentityRole<Guid>
{
    public string? Description { get; set; }
    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}

// ─── Refresh Token Record ─────────────────────────────────────────────────────

public class RefreshTokenRecord : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsActive => !IsRevoked && DateTime.UtcNow < ExpiresAt;
}

// ─── Database Backup ──────────────────────────────────────────────────────────

public class DatabaseBackup : BaseEntity
{
    public Guid DatabaseInstanceId { get; set; }
    public string FileName { get; set; } = default!;
    public string? StoragePath { get; set; }
    public long SizeBytes { get; set; }
    public string Status { get; set; } = "pending"; // pending|completed|failed
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsAutomatic { get; set; }
    public bool IsManual => !IsAutomatic;
    public Guid? S3DestinationId { get; set; }
    public DatabaseInstance DatabaseInstance { get; set; } = default!;
}

// ─── Scheduled Deploy ─────────────────────────────────────────────────────────

public class ScheduledDeploy : TenantEntity
{
    public Guid ProjectId { get; set; }
    public string CronExpression { get; set; } = default!;
    public bool IsEnabled { get; set; } = true;
    public string? Branch { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string TimeZone { get; set; } = "UTC";
}

// ─── Preview Environment (PR deploys) ────────────────────────────────────────

public class PreviewEnvironment : TenantEntity
{
    public Guid ProjectId { get; set; }
    public string PrNumber { get; set; } = default!;
    public string PrTitle { get; set; } = default!;
    public string Branch { get; set; } = default!;
    public string? Url { get; set; }
    public string Status { get; set; } = "pending";
    public Guid? DeploymentId { get; set; }
    public DateTime? MergedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

// ─── S3 Destination ───────────────────────────────────────────────────────────

public class S3Destination : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Endpoint { get; set; } = default!;
    public string BucketName { get; set; } = default!;
    public string AccessKeyIdEncrypted { get; set; } = default!;
    public string SecretAccessKeyEncrypted { get; set; } = default!;
    public string? Region { get; set; }
    public bool IsDefault { get; set; } = false;
    public S3DestinationStatus Status { get; set; } = S3DestinationStatus.Unconfigured;
    public DateTime? LastTestedAt { get; set; }
}

public enum S3DestinationStatus { Unconfigured, Active, Error }

// ─── Backup Policy ────────────────────────────────────────────────────────────

public class BackupPolicy : TenantEntity
{
    public Guid DatabaseInstanceId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string CronExpression { get; set; } = "0 2 * * *"; // Default: daily at 2 AM
    public int RetentionDays { get; set; } = 7;
    public Guid? S3DestinationId { get; set; }
    public string StorageLocation { get; set; } = "local"; // "local" or "s3"
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? ErrorMessage { get; set; }
}

// ─── Restore Job ──────────────────────────────────────────────────────────────

public class RestoreJob : TenantEntity
{
    public Guid DatabaseInstanceId { get; set; }
    public Guid BackupId { get; set; }
    public string TargetDatabaseName { get; set; } = default!;
    public RestoreJobStatus Status { get; set; } = RestoreJobStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public float? ProgressPercent { get; set; }
}

public enum RestoreJobStatus { Pending, Running, Success, Failed, Cancelled }

// ─── Policy Template (Approval governance) ───────────────────────────────────

/// <summary>
/// Defines deployment approval rules that can be applied to one or more environments.
/// When a deployment targets a protected environment and a PolicyTemplate applies,
/// <see cref="Deployment.Approve"/> must be called the required number of times before
/// the runner processes the deployment.
/// </summary>
public class PolicyTemplate : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    /// <summary>Comma-separated environment slugs this policy applies to, e.g. "production,staging".</summary>
    public string AppliesTo { get; set; } = "production";
    /// <summary>Number of distinct approvals required before the deployment may proceed.</summary>
    public int RequiredApprovals { get; set; } = 1;
    /// <summary>If set, only users with this Role may approve.</summary>
    public string? RequiredApproverRole { get; set; }
    /// <summary>Auto-approve deployments matching this branch pattern (regex). Null = never auto-approve.</summary>
    public string? AutoApprovePattern { get; set; }
    /// <summary>Block deployments outside these hours (UTC). Null = no restriction. Format: "09:00-17:00".</summary>
    public string? AllowedHoursUtc { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Guid? ProjectId { get; set; }   // null = tenant-wide
}

// ─── SLO (Service Level Objective) ───────────────────────────────────────────

public class ProjectSlo : TenantEntity
{
    public Guid ProjectId { get; set; }
    /// <summary>Target uptime percentage over the rolling 30-day window, e.g. 99.9</summary>
    public double UptimeTargetPercent { get; set; } = 99.9;
    /// <summary>Maximum acceptable p95 response latency in milliseconds.</summary>
    public int P95LatencyMs { get; set; } = 500;
    /// <summary>Acceptable error rate percentage, e.g. 0.1 = 0.1%</summary>
    public double ErrorRateBudgetPercent { get; set; } = 0.1;
    /// <summary>Rolling window in days over which SLO is evaluated.</summary>
    public int WindowDays { get; set; } = 30;
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastEvaluatedAt { get; set; }
    /// <summary>Most recent calculated uptime percentage.</summary>
    public double? CurrentUptimePercent { get; set; }
    /// <summary>Current error budget remaining (% of budget not yet consumed).</summary>
    public double? ErrorBudgetRemainingPercent { get; set; }
}

