using DeployFlow.Domain.Common;
using DeployFlow.Domain.Entities;
using System.Linq.Expressions;

namespace DeployFlow.Domain.Interfaces;

public interface IRepository<TEntity> where TEntity : BaseEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(TEntity entity, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
}

public interface IUnitOfWork : IDisposable
{
    IProjectRepository Projects { get; }
    IDeploymentRepository Deployments { get; }
    IServerRepository Servers { get; }
    IServiceRepository Services { get; }
    IDatabaseRepository Databases { get; }
    IDatabaseBackupRepository DatabaseBackups { get; }
    ITenantRepository<S3Destination> S3Destinations { get; }
    ITenantRepository<BackupPolicy> BackupPolicies { get; }
    ITenantRepository<RestoreJob> RestoreJobs { get; }
    IPipelineRepository Pipelines { get; }
    IAuditLogRepository AuditLogs { get; }
    IAlertRepository Alerts { get; }
    IUserRepository Users { get; }
    ITenantEntityRepository Tenants { get; }
    ISshKeyRepository SshKeys { get; }
    ICostRecordRepository CostRecords { get; }
    IDomainRepository Domains { get; }
    IEnvVariableRepository EnvVariables { get; }
    INotificationConfigRepository NotificationConfigs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}

public interface ITenantRepository<TEntity> : IRepository<TEntity> where TEntity : TenantEntity
{
    Task<IReadOnlyList<TEntity>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
