using DeployFlow.Domain.Common;
using DeployFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace DeployFlow.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.Property(x => x.Plan).HasConversion<string>().HasMaxLength(50).IsRequired();
        b.Property(x => x.SsoIssuer).HasMaxLength(500);
        b.Property(x => x.SsoClientId).HasMaxLength(200);
        b.Property(x => x.SsoClientSecret).HasMaxLength(1000);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);

        b.Property(x => x.RepositoryUrl).HasMaxLength(500);
        b.Property(x => x.RepositoryBranch).HasMaxLength(200);
        b.Property(x => x.BuildCommand).HasMaxLength(500);
        b.Property(x => x.StartCommand).HasMaxLength(500);
        b.Property(x => x.InstallCommand).HasMaxLength(500);
        b.Property(x => x.DockerfilePath).HasMaxLength(500);
        b.Property(x => x.CustomDomain).HasMaxLength(500);
        b.Property(x => x.Framework).HasMaxLength(100);
        b.Property(x => x.AutoDeployEnabled);

        // Oracle: serialize List<string> as NCLOB — no native array type
        b.Property(x => x.Tags)
            .HasColumnType("NCLOB")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? new List<string>() : (JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()),
                new ValueComparer<List<string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v != null ? v.GetHashCode() : 0)),
                    c => c.ToList()));
        b.Property(x => x.ActiveSlot).HasMaxLength(10).HasDefaultValue("blue");
        b.Property(x => x.BlueContainerName).HasMaxLength(300).IsRequired(false);
        b.Property(x => x.GreenContainerName).HasMaxLength(300).IsRequired(false);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.UpdatedAt });
    }
}

public class ProjectTagConfiguration : IEntityTypeConfiguration<ProjectTag>
{
    public void Configure(EntityTypeBuilder<ProjectTag> b)
    {
        b.ToTable("project_tags");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
    }
}

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.ToTable("Services");
        b.HasKey(x => x.Id);
        b.Property(x => x.MinReplicas).HasDefaultValue(1);
        b.Property(x => x.MaxReplicas).HasDefaultValue(1);
        b.Property(x => x.CpuTargetPercentage).IsRequired(false);
        b.Property(x => x.MemoryTargetPercentage).IsRequired(false);
        b.Property(x => x.LastScaledAt).IsRequired(false);
        b.Property(x => x.LastScalingAction).HasMaxLength(50);
        b.Property(x => x.LastScalingReason).HasMaxLength(1000);
        b.HasIndex(x => x.ProjectId);
    }
}

public class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> b)
    {
        b.ToTable("deployments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.CommitSha).HasMaxLength(100);
        b.Property(x => x.CommitMessage).HasMaxLength(1000);
        b.Property(x => x.CommitAuthor).HasMaxLength(200);
        b.Property(x => x.Branch).HasMaxLength(200);
        b.Property(x => x.Version).HasMaxLength(100);
        b.Property(x => x.ImageTag).HasMaxLength(200);
        b.Property(x => x.Url).HasMaxLength(500);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.HasMany(x => x.Logs).WithOne(l => l.Deployment).HasForeignKey(l => l.DeploymentId).OnDelete(DeleteBehavior.Cascade);
        // Oracle has no native JSON column — serialize Metadata dictionary to NCLOB
        b.Property(x => x.Metadata)
            .HasColumnType("NCLOB")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? new Dictionary<string, string>() : (JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new()),
                new ValueComparer<Dictionary<string, string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                    c => new Dictionary<string, string>(c)));
        b.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.ApprovalNotes).HasMaxLength(1000);
        b.Property(x => x.CanaryStatus).HasConversion<string>().HasMaxLength(50);
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.Status);
        // Composite indexes for dashboard / paged queries
        b.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.TenantId, x.CreatedAt });
        b.HasIndex(x => new { x.ProjectId, x.CreatedAt });
    }
}

public class ProjectEnvironmentConfiguration : IEntityTypeConfiguration<ProjectEnvironment>
{
    public void Configure(EntityTypeBuilder<ProjectEnvironment> b)
    {
        b.ToTable("ProjectEnvironments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired();
        b.Property(x => x.Slug).IsRequired();
        b.Property(x => x.Branch).HasMaxLength(200);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => new { x.ProjectId, x.Branch });
    }
}

public class DeploymentLogConfiguration : IEntityTypeConfiguration<DeploymentLog>
{
    public void Configure(EntityTypeBuilder<DeploymentLog> b)
    {
        b.ToTable("deployment_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Message).HasMaxLength(4000);
        b.Property(x => x.Stream).HasMaxLength(20);
        b.HasIndex(x => x.DeploymentId);
        b.HasIndex(x => x.Timestamp);
        b.HasIndex(x => new { x.DeploymentId, x.Timestamp });
    }
}

public class ServerConfiguration : IEntityTypeConfiguration<Server>
{
    public void Configure(EntityTypeBuilder<Server> b)
    {
        b.ToTable("servers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(100).IsRequired();
        b.Property(x => x.SshUser).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Region).HasMaxLength(100);
        // Oracle: serialize List<string> as NCLOB
        b.Property(x => x.Tags)
            .HasColumnType("NCLOB")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? new List<string>() : (JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()),
                new ValueComparer<List<string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v != null ? v.GetHashCode() : 0)),
                    c => c.ToList()));
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.Status });
        // IsCordoned/IsDraining are indexed for cluster node selection filtering
        b.HasIndex(x => new { x.Id, x.IsCordoned });
    }
}

public class ServerMetricsConfiguration : IEntityTypeConfiguration<ServerMetrics>
{
    public void Configure(EntityTypeBuilder<ServerMetrics> b)
    {
        b.ToTable("server_metrics");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasIndex(x => new { x.ServerId, x.Timestamp });
    }
}

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.AvatarUrl).HasColumnType("CLOB");  // base64 images can be large
        b.Property(x => x.Role).HasMaxLength(50).IsRequired();
        b.Property(x => x.RefreshToken).HasMaxLength(500);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.RefreshToken); // no partial-index filter — Oracle uses a regular index
    }
}

public class TeamInvitationConfiguration : IEntityTypeConfiguration<TeamInvitation>
{
    public void Configure(EntityTypeBuilder<TeamInvitation> b)
    {
        b.ToTable("team_invitations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Role).HasMaxLength(50).IsRequired();
        b.Property(x => x.Token).HasMaxLength(100).IsRequired();
        b.Property(x => x.InvitedByName).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.Email });
        b.HasIndex(x => x.Token).IsUnique();
    }
}

public class SshKeyConfiguration : IEntityTypeConfiguration<SshKey>
{
    public void Configure(EntityTypeBuilder<SshKey> b)
    {
        b.ToTable("ssh_keys");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.PrivateKeyEncrypted).HasColumnType("NCLOB").IsRequired(); // CLOB for large SSH key content
        b.Property(x => x.PublicKey).HasMaxLength(4000);
        b.HasIndex(x => x.TenantId);
    }
}

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> b)
    {
        b.ToTable("alerts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Condition).HasMaxLength(500);
        b.Property(x => x.Threshold).HasMaxLength(200);
        b.HasIndex(x => x.TenantId);
        // Composite for alert dedup query in AlertEvaluatorService: (TenantId, Source, ResourceId, Status)
        b.HasIndex(x => new { x.TenantId, x.Source, x.ResourceId, x.Status });
    }
}

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> b)
    {
        b.ToTable("alert_rules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Metric).HasMaxLength(100).IsRequired();
        b.Property(x => x.Operator).HasMaxLength(8).IsRequired();
        b.Property(x => x.Threshold).HasPrecision(18, 4);
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.IsEnabled });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserName).HasMaxLength(200);
        b.Property(x => x.Action).HasMaxLength(200).IsRequired();
        b.Property(x => x.ResourceType).HasMaxLength(100).IsRequired();
        b.Property(x => x.ResourceName).HasMaxLength(500);
        b.Property(x => x.IpAddress).HasMaxLength(50);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.Property(x => x.MetadataJson).HasColumnType("NCLOB"); // Oracle: NCLOB instead of jsonb
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.CreatedAt);
    }
}

public class DatabaseInstanceConfiguration : IEntityTypeConfiguration<DatabaseInstance>
{
    public void Configure(EntityTypeBuilder<DatabaseInstance> b)
    {
        b.ToTable("database_instances");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Engine).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Version).HasMaxLength(50);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Host).HasMaxLength(500);
        b.Property(x => x.DatabaseName).HasMaxLength(200);
        b.Property(x => x.Username).HasMaxLength(200);
        b.Property(x => x.PasswordEncrypted).HasMaxLength(500);
        // Oracle: serialize List<string> as NCLOB — no native array type
        b.Property(x => x.Tags)
            .HasColumnType("NCLOB")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? new List<string>() : (JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()),
                new ValueComparer<List<string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v != null ? v.GetHashCode() : 0)),
                    c => c.ToList()));
        b.HasIndex(x => x.TenantId);
    }
}

public class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> b)
    {
        b.ToTable("pipelines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(50);
        b.HasMany(x => x.Stages).WithOne().HasForeignKey("PipelineId").OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}

public class CostRecordConfiguration : IEntityTypeConfiguration<CostRecord>
{
    public void Configure(EntityTypeBuilder<CostRecord> b)
    {
        b.ToTable("cost_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.ResourceType).HasMaxLength(100).IsRequired();
        b.Property(x => x.ResourceName).HasMaxLength(500);
        b.Property(x => x.Amount).HasPrecision(18, 4);
        b.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("USD");
        b.Property(x => x.Period).HasMaxLength(50);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.RecordedAt });
    }
}

public class VolumeConfiguration : IEntityTypeConfiguration<Volume>
{
    public void Configure(EntityTypeBuilder<Volume> b)
    {
        b.ToTable("volumes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Driver).HasMaxLength(100).HasDefaultValue("local");
        b.Property(x => x.MountPath).HasMaxLength(500);
        b.Property(x => x.DockerName).HasMaxLength(300);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.Name });
        b.HasIndex(x => x.ServerId);
    }
}

public class PipelineRunConfiguration : IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(EntityTypeBuilder<PipelineRun> b)
    {
        b.ToTable("pipeline_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.TriggeredBy).HasMaxLength(200);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.PipelineId, x.StartedAt });
    }
}

public class PipelineRunLogConfiguration : IEntityTypeConfiguration<PipelineRunLog>
{
    public void Configure(EntityTypeBuilder<PipelineRunLog> b)
    {
        b.ToTable("pipeline_run_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Level).HasMaxLength(20).IsRequired();
        b.Property(x => x.StageName).HasMaxLength(120).IsRequired();
        b.Property(x => x.StepName).HasMaxLength(120);
        b.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        b.HasIndex(x => new { x.PipelineRunId, x.Sequence });
    }
}

public class S3DestinationConfiguration : IEntityTypeConfiguration<S3Destination>
{
    public void Configure(EntityTypeBuilder<S3Destination> b)
    {
        b.ToTable("s3_destinations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Endpoint).HasMaxLength(500).IsRequired();
        b.Property(x => x.BucketName).HasMaxLength(100).IsRequired();
        b.Property(x => x.AccessKeyIdEncrypted).HasMaxLength(1000).IsRequired();
        b.Property(x => x.SecretAccessKeyEncrypted).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Region).HasMaxLength(50);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.IsDefault });
    }
}

public class BackupPolicyConfiguration : IEntityTypeConfiguration<BackupPolicy>
{
    public void Configure(EntityTypeBuilder<BackupPolicy> b)
    {
        b.ToTable("backup_policies");
        b.HasKey(x => x.Id);
        b.Property(x => x.CronExpression).HasMaxLength(100).HasDefaultValue("0 2 * * *");
        b.Property(x => x.StorageLocation).HasMaxLength(50).HasDefaultValue("local");
        b.Property(x => x.ErrorMessage).HasMaxLength(1000);
        b.HasIndex(x => new { x.TenantId, x.DatabaseInstanceId }).IsUnique();
        b.HasIndex(x => x.S3DestinationId);
    }
}

public class RestoreJobConfiguration : IEntityTypeConfiguration<RestoreJob>
{
    public void Configure(EntityTypeBuilder<RestoreJob> b)
    {
        b.ToTable("restore_jobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.TargetDatabaseName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.DatabaseInstanceId });
        b.HasIndex(x => x.BackupId);
    }
}

// ─── Sprint 9 Entities ────────────────────────────────────────────────────────

public class AppTemplateConfiguration : IEntityTypeConfiguration<AppTemplate>
{
    public void Configure(EntityTypeBuilder<AppTemplate> b)
    {
        b.ToTable("app_templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Category).HasMaxLength(100).IsRequired();
        b.Property(x => x.DockerImage).HasMaxLength(500).IsRequired();
        b.Property(x => x.ComposeYaml).HasColumnType("NCLOB");
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.DocumentationUrl).HasMaxLength(500);
        b.Property(x => x.GithubUrl).HasMaxLength(500);
        b.Property(x => x.ServiceType).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.DefaultDatabaseType).HasMaxLength(100);
        // Oracle: serialize List<TemplateEnvVar> as NCLOB JSON
        b.Property(x => x.EnvVariables)
            .HasColumnType("NCLOB")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? new List<TemplateEnvVar>()
                     : (JsonSerializer.Deserialize<List<TemplateEnvVar>>(v, (JsonSerializerOptions?)null) ?? new()),
                new ValueComparer<List<TemplateEnvVar>>(
                    (c1, c2) => c1 != null && c2 != null && c1.Count == c2.Count,
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode())),
                    c => c.ToList()));
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.Category);
    }
}

public class ComposeStackConfiguration : IEntityTypeConfiguration<ComposeStack>
{
    public void Configure(EntityTypeBuilder<ComposeStack> b)
    {
        b.ToTable("compose_stacks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ComposeYaml).HasColumnType("NCLOB").IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.EnvironmentName).HasMaxLength(50);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class TraefikRouterConfiguration : IEntityTypeConfiguration<TraefikRouter>
{
    public void Configure(EntityTypeBuilder<TraefikRouter> b)
    {
        b.ToTable("traefik_routers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Rule).HasMaxLength(500).IsRequired();
        b.Property(x => x.ServiceName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Entrypoints).HasMaxLength(100);
        b.Property(x => x.CertResolver).HasMaxLength(100);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.IsEnabled });
    }
}

public class ProvisioningJobConfiguration : IEntityTypeConfiguration<ProvisioningJob>
{
    public void Configure(EntityTypeBuilder<ProvisioningJob> b)
    {
        b.ToTable("provisioning_jobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        b.Property(x => x.Region).HasMaxLength(100).IsRequired();
        b.Property(x => x.Size).HasMaxLength(100).IsRequired();
        b.Property(x => x.Os).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.ProviderServerId).HasMaxLength(200);
        b.Property(x => x.AssignedIpAddress).HasMaxLength(100);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.Property(x => x.PlanOutput).HasColumnType("NCLOB");
        // Oracle: serialize Dictionary<string,string> as NCLOB JSON
        b.Property(x => x.Tags)
            .HasColumnType("NCLOB")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? new Dictionary<string, string>()
                     : (JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new()),
                new ValueComparer<Dictionary<string, string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                    c => new Dictionary<string, string>(c)));
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class RecoveryRuleConfiguration : IEntityTypeConfiguration<RecoveryRule>
{
    public void Configure(EntityTypeBuilder<RecoveryRule> b)
    {
        b.ToTable("recovery_rules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(50);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.IsEnabled });
    }
}

// ─── Sprint 10 Entities ───────────────────────────────────────────────────────

public class OutboundWebhookConfigConfiguration : IEntityTypeConfiguration<OutboundWebhookConfig>
{
    public void Configure(EntityTypeBuilder<OutboundWebhookConfig> b)
    {
        b.ToTable("outbound_webhook_configs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Url).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Events).HasColumnType("NCLOB").IsRequired();
        b.Property(x => x.Secret).HasMaxLength(500);
        b.Property(x => x.LastResponseStatus).HasMaxLength(50);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.IsEnabled });
    }
}

public class ProjectDeploymentEnvironmentConfiguration : IEntityTypeConfiguration<ProjectDeploymentEnvironment>
{
    public void Configure(EntityTypeBuilder<ProjectDeploymentEnvironment> b)
    {
        b.ToTable("project_deployment_environments");
        b.HasKey(x => x.Id);
        b.Property(x => x.EnvironmentName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Branch).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => new { x.ProjectId, x.Branch }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.Order });
    }
}
