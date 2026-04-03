using DeployFlow.Domain.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace DeployFlow.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ICurrentUserContext? _currentUser;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserContext? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // Core entities
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTag> ProjectTags => Set<ProjectTag>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<DeploymentLog> DeploymentLogs => Set<DeploymentLog>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<ServerMetrics> ServerMetrics => Set<ServerMetrics>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<DatabaseInstance> Databases => Set<DatabaseInstance>();
    public DbSet<DatabaseBackup> DatabaseBackups => Set<DatabaseBackup>();
    public DbSet<S3Destination> S3Destinations => Set<S3Destination>();
    public DbSet<BackupPolicy> BackupPolicies => Set<BackupPolicy>();
    public DbSet<RestoreJob> RestoreJobs => Set<RestoreJob>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<PipelineStep> PipelineSteps => Set<PipelineStep>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Domain.Entities.Domain> Domains => Set<Domain.Entities.Domain>();
    public DbSet<EnvVariable> EnvVariables => Set<EnvVariable>();
    public DbSet<SshKey> SshKeys => Set<SshKey>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CostRecord> CostRecords => Set<CostRecord>();
    public DbSet<NotificationConfig> NotificationConfigs => Set<NotificationConfig>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    public DbSet<ResourcePermission> ResourcePermissions => Set<ResourcePermission>();
    public DbSet<Cluster> Clusters => Set<Cluster>();
    public DbSet<DeployWebhook> DeployWebhooks => Set<DeployWebhook>();
    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();
    public DbSet<Volume> Volumes => Set<Volume>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<PipelineRunLog> PipelineRunLogs => Set<PipelineRunLog>();
    public DbSet<ProjectEnvironment> ProjectEnvironments => Set<ProjectEnvironment>();
    public DbSet<ComposeStack> ComposeStacks => Set<ComposeStack>();
    public DbSet<TraefikRouter> TraefikRouters => Set<TraefikRouter>();
    public DbSet<ProvisioningJob> ProvisioningJobs => Set<ProvisioningJob>();
    public DbSet<RecoveryRule> RecoveryRules => Set<RecoveryRule>();
    public DbSet<PreviewEnvironment> PreviewEnvironments => Set<PreviewEnvironment>();
    public DbSet<ProjectDeploymentEnvironment> ProjectDeploymentEnvironments => Set<ProjectDeploymentEnvironment>();
    public DbSet<OutboundWebhookConfig> OutboundWebhookConfigs => Set<OutboundWebhookConfig>();
    public DbSet<PolicyTemplate> PolicyTemplates => Set<PolicyTemplate>();
    public DbSet<ProjectSlo> ProjectSlos => Set<ProjectSlo>();
    // Sprint 11 — Auto-Scaling, Blue/Green, Secrets, Ephemeral Envs, OTel
    public DbSet<ScalingPolicy> ScalingPolicies => Set<ScalingPolicy>();
    public DbSet<ScalingEvent> ScalingEvents => Set<ScalingEvent>();
    public DbSet<BlueGreenDeployment> BlueGreenDeployments => Set<BlueGreenDeployment>();
    public DbSet<BlueGreenSwitchLog> BlueGreenSwitchLogs => Set<BlueGreenSwitchLog>();
    public DbSet<SecretVault> SecretVaults => Set<SecretVault>();
    public DbSet<VaultSecret> VaultSecrets => Set<VaultSecret>();
    public DbSet<SecretProjectBinding> SecretProjectBindings => Set<SecretProjectBinding>();
    public DbSet<SecretAuditEntry> SecretAuditEntries => Set<SecretAuditEntry>();
    public DbSet<EphemeralEnvironment> EphemeralEnvironments => Set<EphemeralEnvironment>();
    public DbSet<OtelTrace> OtelTraces => Set<OtelTrace>();
    public DbSet<LogAggregationRule> LogAggregationRules => Set<LogAggregationRule>();
    public DbSet<CrashReport> CrashReports => Set<CrashReport>();
    // Sprint 12 — DAG execution + Rollback
    public DbSet<PipelineSnapshot> PipelineSnapshots => Set<PipelineSnapshot>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Rename Identity tables
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<ApplicationRole>().ToTable("roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("user_tokens");

        // Global soft-delete filter
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(null, new object[] { builder });
            }
        }
    }

    private static void SetSoftDeleteFilter<T>(ModelBuilder builder) where T : BaseEntity
    {
        builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    if (userId.HasValue) entry.Entity.CreatedBy = userId.Value;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    if (userId.HasValue) entry.Entity.UpdatedBy = userId.Value;
                    break;
            }
        }

        return base.SaveChangesAsync(ct);
    }
}

// Minimal interface so DbContext can read current user without circular dep
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
}
