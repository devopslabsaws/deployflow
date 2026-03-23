using DeployFlow.Domain.Entities;

namespace DeployFlow.Domain.Interfaces;

public interface IProjectRepository : ITenantRepository<Project>
{
    Task<Project?> GetByNameAsync(string name, Guid tenantId, CancellationToken ct = default);
    Task<Project?> GetBySlugAsync(string slug, Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<Project> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? status = null,
        CancellationToken ct = default);
    Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> HasActiveProjectsOnServerAsync(Guid serverId, CancellationToken ct = default);
}

public interface IDeploymentRepository : ITenantRepository<Deployment>
{
    Task<(IReadOnlyList<Deployment> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 20,
        Guid? projectId = null,
        string? status = null,
        string? branch = null,
        CancellationToken ct = default);
    Task<(IReadOnlyList<DeploymentLog> Items, int Total)> GetLogsPagedAsync(
        Guid deploymentId,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default);
    Task<Deployment?> GetLatestForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<Deployment>> GetActiveDeploymentsAsync(CancellationToken ct = default);
    Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> CountByDateAsync(Guid tenantId, DateTime date, CancellationToken ct = default);
    Task<int> CountByRangeAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<DailyDeploymentStat>> GetDailyStatsAsync(
        Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<double> GetAvgDurationSecondsAsync(
        Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default);
}

public interface IServerRepository : ITenantRepository<Server>
{
    Task<IReadOnlyList<Server>> GetOnlineServersAsync(Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<Server> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? provider = null,
        CancellationToken ct = default);
    Task<(int Online, int Total)> GetCountsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<ServerMetrics>> GetMetricsAsync(
        Guid serverId, DateTime since, CancellationToken ct = default);
}

public interface IServiceRepository : ITenantRepository<Service>
{
    Task<IReadOnlyList<Service>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
}

public interface IDatabaseRepository : ITenantRepository<DatabaseInstance>
{
    Task<IReadOnlyList<DatabaseInstance>> GetByServerAsync(Guid serverId, CancellationToken ct = default);
}

public interface IDatabaseBackupRepository : IRepository<DatabaseBackup>
{
    Task<IReadOnlyList<DatabaseBackup>> GetByDatabaseAsync(Guid databaseId, CancellationToken ct = default);
    Task<DatabaseBackup?> GetLatestCompletedAsync(Guid databaseId, CancellationToken ct = default);
}

public interface IPipelineRepository : ITenantRepository<Pipeline>
{
    Task<IReadOnlyList<Pipeline>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
}

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<(IReadOnlyList<AuditLog> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 50,
        string? resourceType = null,
        CancellationToken ct = default);
}

public interface IAlertRepository : ITenantRepository<Alert>
{
    Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<Alert> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 20,
        string? severity = null,
        bool? acknowledged = null,
        CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<ApplicationUser?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationUser>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
}

public interface ITenantEntityRepository : IRepository<Tenant>
{
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
}

public interface ISshKeyRepository : ITenantRepository<SshKey>
{
    new Task<IReadOnlyList<SshKey>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
}

public interface ICostRecordRepository : IRepository<CostRecord>
{
    Task<decimal> GetMonthlyCostAsync(Guid tenantId, int year, int month, CancellationToken ct = default);
    Task<IReadOnlyList<CostRecord>> GetByDateRangeAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default);
}

// Value object returned by deployment stats query
public record DailyDeploymentStat(DateTime Date, int Successful, int Failed, int Cancelled);

// ─── Domain / EnvVariable / Notification Repositories ────────────────────────

public interface IDomainRepository : ITenantRepository<DeployFlow.Domain.Entities.Domain>
{
    Task<DeployFlow.Domain.Entities.Domain?> GetByNameAsync(string name, Guid tenantId, CancellationToken ct = default);
}

public interface IEnvVariableRepository : ITenantRepository<EnvVariable>
{
    Task<IReadOnlyList<EnvVariable>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
}

public interface INotificationConfigRepository : ITenantRepository<NotificationConfig>
{
    Task<IReadOnlyList<NotificationConfig>> GetEnabledByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
