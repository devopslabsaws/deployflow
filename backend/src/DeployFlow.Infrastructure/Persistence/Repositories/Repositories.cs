using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.Infrastructure.Persistence.Repositories;

public class ProjectRepository : TenantRepository<Project>, IProjectRepository
{
    public ProjectRepository(ApplicationDbContext db) : base(db) { }

    public async Task<Project?> GetByNameAsync(string name, Guid tenantId, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Name == name, ct);

    public async Task<Project?> GetBySlugAsync(string slug, Guid tenantId, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Slug == slug, ct);

    public async Task<(IReadOnlyList<Project> Items, int Total)> GetPagedAsync(
        Guid tenantId, int page, int pageSize, string? search, string? status, CancellationToken ct)
    {
        var query = _set.Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProjectStatus>(status, true, out var parsedStatus))
            query = query.Where(p => p.Status == parsedStatus);

        var total = await query.CountAsync(ct);
        var items = await query
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct)
        => await _set.CountAsync(p => p.TenantId == tenantId && p.Status == ProjectStatus.Active, ct);

    public async Task<bool> HasActiveProjectsOnServerAsync(Guid serverId, CancellationToken ct)
        => await _set.AnyAsync(p => p.ServerId == serverId && p.Status == ProjectStatus.Active, ct);
}

public class DeploymentRepository : TenantRepository<Deployment>, IDeploymentRepository
{
    public DeploymentRepository(ApplicationDbContext db) : base(db) { }

    public async Task<(IReadOnlyList<Deployment> Items, int Total)> GetPagedAsync(
        Guid tenantId, int page, int pageSize, Guid? projectId, string? status, string? branch, CancellationToken ct)
    {
        var query = _set
            .Include(d => d.Project)
            .Where(d => d.TenantId == tenantId);

        if (projectId.HasValue)
            query = query.Where(d => d.ProjectId == projectId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DeploymentStatus>(status, true, out var s))
            query = query.Where(d => d.Status == s);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(d => d.Branch == branch);

        var total = await query.CountAsync(ct);
        var items = await query
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<DeploymentLog> Items, int Total)> GetLogsPagedAsync(
        Guid deploymentId, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.DeploymentLogs.Where(l => l.DeploymentId == deploymentId);
        var total = await query.CountAsync(ct);
        var items = await query
            .AsNoTracking()
            .OrderBy(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<Deployment?> GetLatestForProjectAsync(Guid projectId, CancellationToken ct)
        => await _set.Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Deployment>> GetActiveDeploymentsAsync(CancellationToken ct)
        => await _set.Where(d =>
            (d.Status == DeploymentStatus.Queued ||
             d.Status == DeploymentStatus.Building ||
             d.Status == DeploymentStatus.Deploying) &&
            !d.IsDeleted)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct)
        => await _set.CountAsync(d => d.TenantId == tenantId &&
            (d.Status == DeploymentStatus.Building || d.Status == DeploymentStatus.Deploying), ct);

    public async Task<int> CountByDateAsync(Guid tenantId, DateTime date, CancellationToken ct)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        return await _set.CountAsync(d => d.TenantId == tenantId &&
            d.CreatedAt >= start && d.CreatedAt < end, ct);
    }

    public async Task<int> CountByRangeAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct)
        => await _set.CountAsync(d => d.TenantId == tenantId &&
            d.CreatedAt >= from && d.CreatedAt <= to, ct);

    public async Task<IReadOnlyList<DailyDeploymentStat>> GetDailyStatsAsync(
        Guid tenantId, DateTime from, DateTime to, CancellationToken ct)
    {
        var stats = await _set
            .Where(d => d.TenantId == tenantId && d.CreatedAt >= from && d.CreatedAt < to)
            .GroupBy(d => d.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Successful = g.Count(d => d.Status == DeploymentStatus.Healthy),
                Failed = g.Count(d => d.Status == DeploymentStatus.Failed),
                Cancelled = g.Count(d => d.Status == DeploymentStatus.Cancelled)
            })
            .OrderBy(s => s.Date)
            .ToListAsync(ct);

        return stats.Select(s => new DailyDeploymentStat(s.Date, s.Successful, s.Failed, s.Cancelled)).ToList();
    }

    public async Task<double> GetAvgDurationSecondsAsync(
        Guid tenantId, DateTime from, DateTime to, CancellationToken ct)
    {
        var avg = await _set
            .Where(d => d.TenantId == tenantId &&
                        d.CreatedAt >= from && d.CreatedAt < to &&
                        d.Status == DeploymentStatus.Healthy &&
                        d.DurationSeconds > 0)
            .AverageAsync(d => (double?)d.DurationSeconds, ct);
        return avg ?? 0.0;
    }
}

public class ServerRepository : TenantRepository<Server>, IServerRepository
{
    public ServerRepository(ApplicationDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Server>> GetOnlineServersAsync(Guid tenantId, CancellationToken ct)
        => await _set.Where(s => s.TenantId == tenantId && s.Status == ServerStatus.Online)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Server> Items, int Total)> GetPagedAsync(
        Guid tenantId, int page, int pageSize, string? status, string? provider, CancellationToken ct)
    {
        var query = _set.Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ServerStatus>(status, true, out var s))
            query = query.Where(x => x.Status == s);

        if (!string.IsNullOrWhiteSpace(provider) && Enum.TryParse<ServerProvider>(provider, true, out var p))
            query = query.Where(x => x.Provider == p);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(int Online, int Total)> GetCountsAsync(Guid tenantId, CancellationToken ct)
    {
        var total = await _set.CountAsync(s => s.TenantId == tenantId, ct);
        var online = await _set.CountAsync(s => s.TenantId == tenantId && s.Status == ServerStatus.Online, ct);
        return (online, total);
    }

    public async Task<IReadOnlyList<ServerMetrics>> GetMetricsAsync(Guid serverId, DateTime since, CancellationToken ct)
        => await _db.ServerMetrics
            .Where(m => m.ServerId == serverId && m.Timestamp >= since)
            .AsNoTracking()
            .OrderBy(m => m.Timestamp)
            .ToListAsync(ct);
}

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _db;
    public UserRepository(ApplicationDbContext db) { _db = db; }

    public async Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Users.FindAsync(new object[] { id }, ct);

    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct)
        => await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant(), ct);

    public async Task<ApplicationUser?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct)
        => await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, ct);

    public async Task<IReadOnlyList<ApplicationUser>> GetByTenantAsync(Guid tenantId, CancellationToken ct)
        => await _db.Users.Where(u => u.TenantId == tenantId).ToListAsync(ct);
}

public class TenantEntityRepository : Repository<Tenant>, ITenantEntityRepository
{
    public TenantEntityRepository(ApplicationDbContext db) : base(db) { }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct)
        => await _set.FirstOrDefaultAsync(t => t.Slug == slug, ct);
}

public class SshKeyRepository : TenantRepository<SshKey>, ISshKeyRepository
{
    public SshKeyRepository(ApplicationDbContext db) : base(db) { }

    public new async Task<IReadOnlyList<SshKey>> GetByTenantAsync(Guid tenantId, CancellationToken ct)
        => await _set.Where(k => k.TenantId == tenantId).ToListAsync(ct);
}

public class AlertRepository : TenantRepository<Alert>, IAlertRepository
{
    public AlertRepository(ApplicationDbContext db) : base(db) { }

    public async Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct)
        => await _set.CountAsync(a => a.TenantId == tenantId && a.AcknowledgedBy == null, ct);

    public async Task<(IReadOnlyList<Alert> Items, int Total)> GetPagedAsync(
        Guid tenantId, int page, int pageSize, string? severity, bool? acknowledged, CancellationToken ct)
    {
        var query = _set.Where(a => a.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<AlertSeverity>(severity, true, out var sev))
            query = query.Where(a => a.Severity == sev);
        if (acknowledged.HasValue)
            query = acknowledged.Value
                ? query.Where(a => a.AcknowledgedBy != null)
                : query.Where(a => a.AcknowledgedBy == null);

        var total = await query.CountAsync(ct);
        var items = await query
            .AsNoTracking()
            .OrderByDescending(a => a.TriggeredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }
}

public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(ApplicationDbContext db) : base(db) { }

    public async Task<(IReadOnlyList<AuditLog> Items, int Total)> GetPagedAsync(
        Guid tenantId, int page, int pageSize, string? resourceType, CancellationToken ct)
    {
        var query = _db.AuditLogs.Where(l => l.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(resourceType))
            query = query.Where(l => l.ResourceType == resourceType);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }
}

public class CostRecordRepository : Repository<CostRecord>, ICostRecordRepository
{
    public CostRecordRepository(ApplicationDbContext db) : base(db) { }

    public async Task<decimal> GetMonthlyCostAsync(Guid tenantId, int year, int month, CancellationToken ct)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        return await _db.CostRecords
            .Where(r => r.TenantId == tenantId && r.RecordedAt >= start && r.RecordedAt < end)
            .SumAsync(r => r.Amount, ct);
    }

    public async Task<IReadOnlyList<CostRecord>> GetByDateRangeAsync(
        Guid tenantId, DateTime from, DateTime to, CancellationToken ct)
    {
        return await _db.CostRecords
            .Where(r => r.TenantId == tenantId && r.RecordedAt >= from && r.RecordedAt <= to)
            .AsNoTracking()
            .OrderBy(r => r.RecordedAt)
            .ToListAsync(ct);
    }
}
