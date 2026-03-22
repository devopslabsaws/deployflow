using DeployFlow.Domain.Common;
using DeployFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DeployFlow.Infrastructure.Persistence.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly ApplicationDbContext _db;
    protected readonly DbSet<TEntity> _set;

    public Repository(ApplicationDbContext db)
    {
        _db = db;
        _set = db.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
        => await _set.ToListAsync(ct);

    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await _set.Where(predicate).ToListAsync(ct);

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _set.AddAsync(entity, ct);
        return entity;
    }

    public virtual Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        _set.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(TEntity entity, CancellationToken ct = default)
    {
        entity.SoftDelete(Guid.Empty);
        _set.Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null
            ? await _set.CountAsync(ct)
            : await _set.CountAsync(predicate, ct);

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await _set.AnyAsync(predicate, ct);
}

public class TenantRepository<TEntity> : Repository<TEntity>, ITenantRepository<TEntity>
    where TEntity : TenantEntity
{
    public TenantRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<TEntity>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _set.Where(e => e.TenantId == tenantId).ToListAsync(ct);
}
