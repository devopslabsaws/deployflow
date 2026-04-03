using DeployFlow.Application.Services;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.Infrastructure.Repositories;

/// <summary>EF Core implementation of pipeline snapshot persistence.</summary>
public sealed class PipelineSnapshotRepository : IPipelineSnapshotRepository
{
    private readonly ApplicationDbContext _db;

    public PipelineSnapshotRepository(ApplicationDbContext db) => _db = db;

    public async Task<Guid> AddAsync(PipelineSnapshot snapshot, CancellationToken ct)
    {
        _db.PipelineSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(ct);
        return snapshot.Id;
    }

    public async Task<PipelineSnapshot?> GetLatestHealthyAsync(Guid pipelineId, CancellationToken ct) =>
        await _db.PipelineSnapshots
            .Where(s => s.PipelineId == pipelineId && s.IsHealthy && s.RolledBackAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task UpdateAsync(PipelineSnapshot snapshot, CancellationToken ct)
    {
        _db.PipelineSnapshots.Update(snapshot);
        await _db.SaveChangesAsync(ct);
    }

    public async Task PruneAsync(Guid pipelineId, int keep, CancellationToken ct)
    {
        var toDelete = await _db.PipelineSnapshots
            .Where(s => s.PipelineId == pipelineId)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(keep)
            .ToListAsync(ct);

        if (toDelete.Count > 0)
        {
            _db.PipelineSnapshots.RemoveRange(toDelete);
            await _db.SaveChangesAsync(ct);
        }
    }
}
