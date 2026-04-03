using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

// ─── Feature 1: Vertical & Horizontal Auto-Scaling ──────────────────────────

public enum ScalingDirection { Up, Down, None }
public enum ScalingTrigger { CpuThreshold, MemoryThreshold, Manual, Schedule, ScaleToZero }

public class ScalingPolicy : TenantEntity
{
    public string Name { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? ServerId { get; set; }
    public string ContainerName { get; set; } = default!;     // docker-compose service name

    // Horizontal scaling (replicas)
    public bool HorizontalEnabled { get; set; } = false;
    public int MinReplicas { get; set; } = 1;
    public int MaxReplicas { get; set; } = 5;
    public int CurrentReplicas { get; set; } = 1;
    public int CpuScaleUpThreshold { get; set; } = 80;        // % to trigger scale-up
    public int CpuScaleDownThreshold { get; set; } = 30;      // % to trigger scale-down
    public int MemoryScaleUpThreshold { get; set; } = 85;
    public int MemoryScaleDownThreshold { get; set; } = 40;
    public int ScaleCooldownSeconds { get; set; } = 120;       // min time between scale events

    // Vertical scaling (resource limits)
    public bool VerticalEnabled { get; set; } = false;
    public string? CpuLimit { get; set; }                      // e.g. "0.5", "2"
    public string? MemoryLimit { get; set; }                   // e.g. "512m", "2g"
    public string? CpuRequest { get; set; }
    public string? MemoryRequest { get; set; }

    // Scale-to-zero (dev environments)
    public bool ScaleToZeroEnabled { get; set; } = false;
    public int ScaleToZeroAfterMinutes { get; set; } = 30;    // idle time before scale to 0
    public DateTime? LastActivityAt { get; set; }
    public bool IsScaledToZero { get; set; } = false;

    public bool IsActive { get; set; } = true;
    public DateTime? LastScaledAt { get; set; }
    public ScalingDirection LastScalingDirection { get; set; } = ScalingDirection.None;
}

public class ScalingEvent : TenantEntity
{
    public Guid PolicyId { get; set; }
    public Guid ProjectId { get; set; }
    public ScalingDirection Direction { get; set; }
    public ScalingTrigger Trigger { get; set; }
    public int FromReplicas { get; set; }
    public int ToReplicas { get; set; }
    public double CpuPercentAtTime { get; set; }
    public double MemoryPercentAtTime { get; set; }
    public string? Notes { get; set; }
    public bool Succeeded { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

// ─── Feature 2: Blue/Green Deployments ──────────────────────────────────────

public enum BlueGreenStatus
{
    Idle,          // no active switch in progress
    Provisioning,  // new (green) container being built
    HealthChecking,// green is up, health-checking before switch
    Switching,     // Traefik router being swapped
    Live,          // green is now live (was blue, now green is blue for next cycle)
    RollingBack,   // reverting router to previous container
    Failed
}

public class BlueGreenDeployment : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ServerId { get; set; }
    public BlueGreenStatus Status { get; set; } = BlueGreenStatus.Idle;

    // Blue slot (currently live)
    public string? BlueContainerId { get; set; }
    public string? BlueContainerName { get; set; }
    public int BluePort { get; set; }
    public string? BlueImageTag { get; set; }
    public string? BlueCommitSha { get; set; }

    // Green slot (new version, under test)
    public string? GreenContainerId { get; set; }
    public string? GreenContainerName { get; set; }
    public int GreenPort { get; set; }
    public string? GreenImageTag { get; set; }
    public string? GreenCommitSha { get; set; }

    // Router
    public string? TraefikRouterName { get; set; }          // Traefik service routing to active slot
    public string? ActiveSlot { get; set; } = "blue";       // "blue" | "green"
    public int HealthCheckRetries { get; set; } = 5;
    public int HealthCheckIntervalSeconds { get; set; } = 10;
    public string? HealthCheckPath { get; set; } = "/health";

    // Traffic splitting (canary-style during switch)
    public int GreenTrafficPercent { get; set; } = 0;       // 0 = instant cut-over
    public bool AutoPromote { get; set; } = true;

    public DateTime? SwitchStartedAt { get; set; }
    public DateTime? SwitchedAt { get; set; }
    public string? LastError { get; set; }
    public Guid? SourceDeploymentId { get; set; }
}

public class BlueGreenSwitchLog : BaseEntity
{
    public Guid BlueGreenId { get; set; }
    public string Message { get; set; } = default!;
    public string? Level { get; set; } = "info";           // info | warn | error
}

// ─── Feature 3: Secret Management (Vault-like) ──────────────────────────────

public enum SecretType { PlainText, Base64, Json, Certificate }
public enum SecretRotationPolicy { Never, Daily, Weekly, Monthly }

public class SecretVault : TenantEntity
{
    public string Name { get; set; } = default!;           // "Production Secrets"
    public string? Description { get; set; }
    public bool IsLocked { get; set; } = false;            // locked = no reads without explicit unlock
    public int SecretCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime? LastRotatedAt { get; set; }
}

public class VaultSecret : TenantEntity
{
    public Guid VaultId { get; set; }
    public string Key { get; set; } = default!;            // ENV_VAR_NAME style
    public string EncryptedValue { get; set; } = default!; // AES-256 encrypted + base64
    public string? Description { get; set; }
    public SecretType SecretType { get; set; } = SecretType.PlainText;
    public SecretRotationPolicy RotationPolicy { get; set; } = SecretRotationPolicy.Never;
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public int Version { get; set; } = 1;
    public string? Tags { get; set; }                      // JSON array ["db","prod"]

    // References — which projects use this secret
    public ICollection<SecretProjectBinding> Bindings { get; set; } = new List<SecretProjectBinding>();
}

public class SecretProjectBinding : BaseEntity
{
    public Guid SecretId { get; set; }
    public Guid ProjectId { get; set; }
    public string? OverrideKey { get; set; }   // inject under a different var name in this project
    public bool InjectAtRuntime { get; set; } = true;
}

public class SecretAuditEntry : TenantEntity
{
    public Guid SecretId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;   // "read" | "write" | "rotate" | "delete"
    public string? IpAddress { get; set; }
    public bool Success { get; set; } = true;
}

// ─── Feature 4: Ephemeral Preview Environment (TTL enhancements) ────────────

public enum EphemeralStatus
{
    Pending,       // PR opened, waiting to provision
    Provisioning,
    Running,
    Sleeping,      // scaled to zero between checks
    Destroying,
    Destroyed,
    Failed
}

public class EphemeralEnvironment : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ServerId { get; set; }
    public string PrNumber { get; set; } = default!;          // e.g. "42"
    public string PrTitle { get; set; } = default!;
    public string PrUrl { get; set; } = default!;
    public string Branch { get; set; } = default!;
    public string CommitSha { get; set; } = default!;
    public string? AuthorName { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public EphemeralStatus Status { get; set; } = EphemeralStatus.Pending;
    public string? PreviewUrl { get; set; }
    public int AssignedPort { get; set; }
    public string? ContainerId { get; set; }
    public string? ContainerName { get; set; }

    // TTL
    public int TtlHours { get; set; } = 24;
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public DateTime? DestroyedAt { get; set; }

    // Lifecycle
    public DateTime? ProvisionedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public string? GithubCheckRunId { get; set; }           // for status check updates
    public string? LastError { get; set; }
    public Guid? SourceDeploymentId { get; set; }
}

// ─── Feature 5: OTel Tracing & Log Aggregation ───────────────────────────────

public enum TraceStatus { Ok, Error, Timeout, Cancelled }

public class OtelTrace : TenantEntity
{
    public string TraceId { get; set; } = default!;         // W3C trace-id (32 hex chars)
    public string RootSpanId { get; set; } = default!;
    public string ServiceName { get; set; } = default!;
    public string? Environment { get; set; }                // "production" | "staging"
    public Guid? ProjectId { get; set; }
    public string? OperationName { get; set; }              // e.g. "POST /api/orders"
    public TraceStatus Status { get; set; } = TraceStatus.Ok;
    public long DurationMs { get; set; }
    public int SpanCount { get; set; }
    public int ErrorSpanCount { get; set; }
    public string? HttpMethod { get; set; }
    public string? HttpUrl { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SpansJson { get; set; }                  // NCLOB – full OTel span tree
}

public class LogAggregationRule : TenantEntity
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = default!;
    public string Pattern { get; set; } = default!;         // regex pattern
    public string Severity { get; set; } = "error";        // "panic" | "error" | "warn"
    public bool TriggerAlert { get; set; } = true;
    public bool SummarizeCrash { get; set; } = false;       // RCA hook
    public bool IsActive { get; set; } = true;
    public int MatchCount { get; set; }
    public DateTime? LastMatchedAt { get; set; }
}

public class CrashReport : TenantEntity
{
    public Guid ProjectId { get; set; }
    public string ServiceName { get; set; } = default!;
    public string Pattern { get; set; } = default!;
    public string? RawLogSample { get; set; }               // NCLOB
    public string? RcaSummary { get; set; }                 // AI/rule-based root cause
    public int OccurrenceCount { get; set; } = 1;
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    public bool IsResolved { get; set; } = false;
    public string? Resolution { get; set; }
}
