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
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.Status);
        // Composite indexes for dashboard / paged queries
        b.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.TenantId, x.CreatedAt });
        b.HasIndex(x => new { x.ProjectId, x.CreatedAt });
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
        b.Property(x => x.AvatarUrl).HasMaxLength(1000);
        b.Property(x => x.Role).HasMaxLength(50).IsRequired();
        b.Property(x => x.RefreshToken).HasMaxLength(500);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.RefreshToken); // no partial-index filter — Oracle uses a regular index
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
