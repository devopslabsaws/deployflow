using DeployFlow.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace DeployFlow.Domain.Entities;

public enum UserRole { Admin, DevOps, Developer, Viewer }

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "developer";
    public Guid TenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    public static ApplicationUser Create(
        string name, string email, string passwordHash, Guid tenantId, string role = "owner")
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = passwordHash,
            FullName = name,
            TenantId = tenantId,
            Role = role,
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void SetRefreshToken(string token, DateTime expiry)
    {
        RefreshToken = token;
        RefreshTokenExpiry = expiry;
    }
}

public class Tenant : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public TenantPlan Plan { get; set; } = TenantPlan.Free;
    public string PlanName => Plan.ToString().ToLower();
    public int MaxProjects { get; set; } = 5;
    public int MaxServers { get; set; } = 2;
    public int MaxDeployments { get; set; } = 100;

    // SSO / OIDC config
    public bool SsoEnabled { get; set; } = false;
    public string? SsoIssuer { get; set; }
    public string? SsoClientId { get; set; }
    public string? SsoClientSecret { get; set; }

    public static Tenant Create(string name)
    {
        var slug = System.Text.RegularExpressions.Regex
            .Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
        return new Tenant
        {
            Name = name,
            Slug = slug.Length > 0 ? slug : Guid.NewGuid().ToString("N")[..8],
            Plan = TenantPlan.Free,
            IsActive = true
        };
    }
}

public enum TenantPlan { Free, Pro, Enterprise }

public class EnvVariable : TenantEntity
{
    public string Key { get; set; } = default!;
    public string? Value { get; set; }
    public EnvVarType Type { get; set; } = EnvVarType.PlainText;
    public Guid? ServiceId { get; set; }
    public Guid? ProjectId { get; set; }
    public bool IsShared { get; set; }

    public Service? Service { get; set; }
    public Project? Project { get; set; }
}

public enum EnvVarType { PlainText, Secret, File }

public class SshKey : TenantEntity
{
    public string Name { get; set; } = default!;
    public string PublicKey { get; set; } = default!;
    public string PrivateKeyEncrypted { get; set; } = default!;
    public string Fingerprint { get; set; } = default!;
    public DateTime? LastUsedAt { get; set; }
}

public class Domain : TenantEntity
{
    public string Name { get; set; } = default!;
    public DomainStatus Status { get; set; } = DomainStatus.Pending;
    public Guid? ServiceId { get; set; }
    public bool IsWildcard { get; set; }
    public bool SslEnabled { get; set; }
    public DateTime? SslExpiresAt { get; set; }
    public SslProvider SslProvider { get; set; } = SslProvider.LetsEncrypt;
    public bool DnsVerified { get; set; }
    public bool RedirectWww { get; set; }
}

public enum DomainStatus { Pending, Active, Error, Expired }
public enum SslProvider { LetsEncrypt, Custom, Cloudflare }

public class AuditLog : BaseEntity
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string ResourceType { get; set; } = default!;
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = default!;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? MetadataJson { get; set; }
    public Guid TenantId { get; set; }
}

public class NotificationConfig : TenantEntity
{
    public string Channel { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsEnabled { get; set; } = true;
    public string ConfigJson { get; set; } = "{}";
    public string EventsJson { get; set; } = "[]";
}

public class AlertRule : TenantEntity
{
    public string Name { get; set; } = default!;
    public string Metric { get; set; } = default!;
    public string Operator { get; set; } = ">";
    public decimal Threshold { get; set; }
    public int WindowMinutes { get; set; } = 5;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public bool IsEnabled { get; set; } = true;
    public int CooldownMinutes { get; set; } = 10;
    public DateTime? LastTriggeredAt { get; set; }
    public string? Description { get; set; }
}

public class Alert : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Active;
    public string Source { get; set; } = default!;
    public Guid? ResourceId { get; set; }
    public string? ResourceType { get; set; }
    public string? Condition { get; set; }
    public string? Threshold { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public Guid? AcknowledgedById { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public bool IsAcknowledged => AcknowledgedBy != null;
    public string ConditionsJson { get; set; } = "[]";

    public void Acknowledge(Guid userId, string userName)
    {
        AcknowledgedById = userId;
        AcknowledgedBy = userName;
        AcknowledgedAt = DateTime.UtcNow;
        Status = AlertStatus.Acknowledged;
    }
}

public enum AlertSeverity { Info, Warning, Critical }
public enum AlertStatus { Active, Resolved, Acknowledged }

public class DatabaseInstance : AggregateRoot
{
    public string Name { get; set; } = default!;
    public DatabaseEngine Engine { get; set; }
    public string Version { get; set; } = default!;
    public DatabaseInstanceStatus Status { get; set; } = DatabaseInstanceStatus.Creating;
    public Guid ServerId { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string DatabaseName { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string PasswordEncrypted { get; set; } = default!;
    public int StorageGb { get; set; } = 10;
    public bool BackupEnabled { get; set; } = true;
    public string BackupSchedule { get; set; } = "0 2 * * *";
    public int BackupRetentionDays { get; set; } = 7;
    public DateTime? LastBackupAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? ContainerId { get; set; }
}

public enum DatabaseEngine { PostgreSQL, MySQL, MariaDB, MongoDB, Redis, MsSql, Oracle }
public enum DatabaseInstanceStatus { Creating, Running, Stopped, Restoring, Error }

public enum PipelineTriggerType { Manual, Push, PullRequest, Tag, Schedule }

public class Pipeline : AggregateRoot
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public PipelineStatus Status { get; set; } = PipelineStatus.Idle;
    public bool IsEnabled { get; set; } = true;
    public PipelineTriggerType Trigger { get; set; } = PipelineTriggerType.Manual;
    public string? CronExpression { get; set; }
    public DateTime? LastRunAt { get; set; }
    public TimeSpan? LastDuration { get; set; }
    public int TotalRuns { get; set; } = 0;
    public int SuccessRuns { get; set; } = 0;
    public int FailedRuns { get; set; } = 0;

    public Project Project { get; set; } = default!;
    public ICollection<PipelineStage> Stages { get; set; } = new List<PipelineStage>();
}

public enum PipelineStageStatus { Pending, Running, Success, Failed, Skipped }
public enum PipelineStepType { Command, Docker, Deploy, Test, Notify, Approval }

public class PipelineStage : BaseEntity
{
    public Guid PipelineId { get; set; }
    public string Name { get; set; } = default!;
    public int Order { get; set; }
    public PipelineStageStatus Status { get; set; } = PipelineStageStatus.Pending;
    public bool RunParallel { get; set; }
    /// <summary>Comma-separated stage names this stage depends on (DAG edges).</summary>
    public string? DependsOn { get; set; }
    /// <summary>When true, stage execution continues even if a step fails.</summary>
    public bool ContinueOnFailure { get; set; }
    public ICollection<PipelineStep> Steps { get; set; } = new List<PipelineStep>();
}

public class PipelineStep : BaseEntity
{
    public Guid PipelineStageId { get; set; }
    public string Name { get; set; } = default!;
    public PipelineStepType Type { get; set; } = PipelineStepType.Command;
    public PipelineStageStatus Status { get; set; } = PipelineStageStatus.Pending;
    public string? Command { get; set; }
    public string? Image { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public TimeSpan? Duration { get; set; }
    public int? Timeout { get; set; }
    /// <summary>Number of times to retry a failed step (0 = no retry).</summary>
    public int? RetryCount { get; set; }
}

public enum PipelineStatus { Idle, Running, Success, Failed, Cancelled }

public enum PipelineRunStatus { Queued, Running, Success, Failed, Cancelled }

public class PipelineRun : TenantEntity
{
    public Guid PipelineId { get; set; }
    public PipelineRunStatus Status { get; set; } = PipelineRunStatus.Queued;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int StageCount { get; set; }
    public int StepCount { get; set; }
    public string? TriggeredBy { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PipelineRunLog : BaseEntity
{
    public Guid PipelineRunId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "info";
    public string StageName { get; set; } = "pipeline";
    public string? StepName { get; set; }
    public string Message { get; set; } = default!;
    public int Sequence { get; set; }
}
