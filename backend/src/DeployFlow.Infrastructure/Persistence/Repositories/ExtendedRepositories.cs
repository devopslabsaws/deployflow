using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.Infrastructure.Persistence.Repositories;

public class ServiceRepository : TenantRepository<Service>, IServiceRepository
{
    public ServiceRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Service>> GetByProjectAsync(Guid projectId, CancellationToken ct)
        => await _set.Where(s => s.ProjectId == projectId).ToListAsync(ct);
}

public class DatabaseRepository : TenantRepository<DatabaseInstance>, IDatabaseRepository
{
    public DatabaseRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<DatabaseInstance>> GetByServerAsync(Guid serverId, CancellationToken ct)
        => await _set.Where(d => d.ServerId == serverId).ToListAsync(ct);
}

public class PipelineRepository : TenantRepository<Pipeline>, IPipelineRepository
{
    public PipelineRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Pipeline>> GetByProjectAsync(Guid projectId, CancellationToken ct)
        => await _set.Include(p => p.Stages).ThenInclude(s => s.Steps)
            .Where(p => p.ProjectId == projectId)
            .ToListAsync(ct);
}

public class DomainRepository : TenantRepository<DeployFlow.Domain.Entities.Domain>, IDomainRepository
{
    public DomainRepository(ApplicationDbContext db) : base(db) { }

    public async Task<DeployFlow.Domain.Entities.Domain?> GetByNameAsync(string name, Guid tenantId, CancellationToken ct)
        => await _set.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Name == name, ct);
}

public class EnvVariableRepository : TenantRepository<EnvVariable>, IEnvVariableRepository
{
    public EnvVariableRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<EnvVariable>> GetByProjectAsync(Guid projectId, CancellationToken ct)
        => await _set.Where(e => e.ProjectId == projectId).ToListAsync(ct);
}

public class NotificationConfigRepository : TenantRepository<NotificationConfig>, INotificationConfigRepository
{
    public NotificationConfigRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<NotificationConfig>> GetEnabledByTenantAsync(Guid tenantId, CancellationToken ct)
        => await _set.Where(n => n.TenantId == tenantId && n.IsEnabled).ToListAsync(ct);
}

