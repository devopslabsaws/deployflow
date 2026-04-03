using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DeployFlow.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public IProjectRepository Projects { get; }
    public IDeploymentRepository Deployments { get; }
    public IServerRepository Servers { get; }
    public IServiceRepository Services { get; }
    public IDatabaseRepository Databases { get; }
    public IDatabaseBackupRepository DatabaseBackups { get; }
    public ITenantRepository<S3Destination> S3Destinations { get; }
    public ITenantRepository<BackupPolicy> BackupPolicies { get; }
    public ITenantRepository<RestoreJob> RestoreJobs { get; }
    public ITenantRepository<TeamInvitation> TeamInvitations { get; }
    public IPipelineRepository Pipelines { get; }
    public IAuditLogRepository AuditLogs { get; }
    public IAlertRepository Alerts { get; }
    public IUserRepository Users { get; }
    public ITenantEntityRepository Tenants { get; }
    public ISshKeyRepository SshKeys { get; }
    public ICostRecordRepository CostRecords { get; }
    public IDomainRepository Domains { get; }
    public IEnvVariableRepository EnvVariables { get; }
    public INotificationConfigRepository NotificationConfigs { get; }
    public IAlertRuleRepository AlertRules { get; }
    public ITenantRepository<ProjectEnvironment> Environments { get; }
    public ITenantRepository<ComposeStack> ComposeStacks { get; }
    public ITenantRepository<TraefikRouter> TraefikRouters { get; }
    public ITenantRepository<ProvisioningJob> ProvisioningJobs { get; }
    public ITenantRepository<RecoveryRule> RecoveryRules { get; }
    public ITenantRepository<PreviewEnvironment> PreviewEnvironments { get; }
    public ITenantRepository<ProjectDeploymentEnvironment> ProjectDeploymentEnvironments { get; }
    public ITenantRepository<OutboundWebhookConfig> OutboundWebhooks { get; }
    public ITenantRepository<PolicyTemplate> PolicyTemplates { get; }
    public ITenantRepository<ProjectSlo> ProjectSlos { get; }

    public UnitOfWork(
        ApplicationDbContext db,
        IProjectRepository projects,
        IDeploymentRepository deployments,
        IServerRepository servers,
        IServiceRepository services,
        IDatabaseRepository databases,
        IDatabaseBackupRepository databaseBackups,
        ITenantRepository<S3Destination> s3Destinations,
        ITenantRepository<BackupPolicy> backupPolicies,
        ITenantRepository<RestoreJob> restoreJobs,
        ITenantRepository<TeamInvitation> teamInvitations,
        IPipelineRepository pipelines,
        IAuditLogRepository auditLogs,
        IAlertRepository alerts,
        IUserRepository users,
        ITenantEntityRepository tenants,
        ISshKeyRepository sshKeys,
        ICostRecordRepository costRecords,
        IDomainRepository domains,
        IEnvVariableRepository envVariables,
        INotificationConfigRepository notificationConfigs,
        IAlertRuleRepository alertRules,
        ITenantRepository<ProjectEnvironment> environments,
        ITenantRepository<ComposeStack> composeStacks,
        ITenantRepository<TraefikRouter> traefikRouters,
        ITenantRepository<ProvisioningJob> provisioningJobs,
        ITenantRepository<RecoveryRule> recoveryRules,
        ITenantRepository<PreviewEnvironment> previewEnvironments,
        ITenantRepository<ProjectDeploymentEnvironment> projectDeploymentEnvironments,
        ITenantRepository<OutboundWebhookConfig> outboundWebhooks,
        ITenantRepository<PolicyTemplate> policyTemplates,
        ITenantRepository<ProjectSlo> projectSlos)
    {
        _db = db;
        Projects = projects;
        Deployments = deployments;
        Servers = servers;
        Services = services;
        Databases = databases;
        DatabaseBackups = databaseBackups;
        S3Destinations = s3Destinations;
        BackupPolicies = backupPolicies;
        RestoreJobs = restoreJobs;
        TeamInvitations = teamInvitations;
        Pipelines = pipelines;
        AuditLogs = auditLogs;
        Alerts = alerts;
        Users = users;
        Tenants = tenants;
        SshKeys = sshKeys;
        CostRecords = costRecords;
        Domains = domains;
        EnvVariables = envVariables;
        NotificationConfigs = notificationConfigs;
        AlertRules = alertRules;
        Environments = environments;
        ComposeStacks = composeStacks;
        TraefikRouters = traefikRouters;
        ProvisioningJobs = provisioningJobs;
        RecoveryRules = recoveryRules;
        PreviewEnvironments = previewEnvironments;
        ProjectDeploymentEnvironments = projectDeploymentEnvironments;
        OutboundWebhooks = outboundWebhooks;
        PolicyTemplates = policyTemplates;
        ProjectSlos = projectSlos;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _db.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            await _transaction.CommitAsync(ct);
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            await _transaction.RollbackAsync(ct);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction?.Dispose();
            _db.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
