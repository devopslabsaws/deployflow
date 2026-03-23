using System.Collections.Concurrent;
using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DeployFlow.Infrastructure.Services;

public class DatabaseRestoreJobService : IDatabaseRestoreJobService
{
    private readonly ConcurrentDictionary<Guid, DatabaseRestoreJobDto> _jobs = new();
    private readonly ConcurrentDictionary<Guid, Guid> _activeByDatabase = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseRestoreJobService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<bool> HasActiveRestoreAsync(Guid databaseId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_activeByDatabase.ContainsKey(databaseId));
    }

    public Task<DatabaseRestoreJobDto?> GetAsync(Guid databaseId, Guid jobId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_jobs.TryGetValue(jobId, out var job)) return Task.FromResult<DatabaseRestoreJobDto?>(null);
        if (job.DatabaseId != databaseId) return Task.FromResult<DatabaseRestoreJobDto?>(null);
        return Task.FromResult<DatabaseRestoreJobDto?>(job);
    }

    public Task<DatabaseRestoreJobDto> StartAsync(
        Guid databaseId,
        Guid backupId,
        string targetDatabaseName,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var existing = _activeByDatabase.GetValueOrDefault(databaseId);
        if (existing != Guid.Empty && _jobs.TryGetValue(existing, out var running) &&
            (running.Status == "queued" || running.Status == "running"))
        {
            return Task.FromResult(running);
        }

        var job = new DatabaseRestoreJobDto(
            Guid.NewGuid(),
            databaseId,
            backupId,
            "queued",
            0,
            "Restore job queued",
            targetDatabaseName,
            DateTime.UtcNow,
            null);

        _jobs[job.JobId] = job;
        _activeByDatabase[databaseId] = job.JobId;

        _ = Task.Run(async () => await RunRestoreSimulationAsync(job.JobId), CancellationToken.None);

        return Task.FromResult(job);
    }

    private async Task RunRestoreSimulationAsync(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job)) return;

        try
        {
            Update(job with { Status = "running", ProgressPercent = 5, Message = "Preparing restore target" });
            await Task.Delay(700);

            Update(job with { Status = "running", ProgressPercent = 20, Message = "Validating backup artifact" });
            await Task.Delay(900);

            Update(job with { Status = "running", ProgressPercent = 45, Message = "Restoring schema" });
            await Task.Delay(1300);

            Update(job with { Status = "running", ProgressPercent = 70, Message = "Restoring data" });
            await Task.Delay(1700);

            Update(job with { Status = "running", ProgressPercent = 90, Message = "Running post-restore checks" });
            await Task.Delay(900);

            Update(job with
            {
                Status = "completed",
                ProgressPercent = 100,
                Message = "Restore completed successfully",
                CompletedAt = DateTime.UtcNow
            });

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var instance = await db.Databases.FindAsync(job.DatabaseId);
            if (instance is not null)
            {
                instance.Status = DatabaseInstanceStatus.Running;
                instance.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        catch
        {
            if (_jobs.TryGetValue(jobId, out var failed))
            {
                Update(failed with
                {
                    Status = "failed",
                    Message = "Restore failed",
                    CompletedAt = DateTime.UtcNow
                });
            }
        }
        finally
        {
            if (_jobs.TryGetValue(jobId, out var current))
            {
                _activeByDatabase.TryRemove(current.DatabaseId, out _);
            }
        }
    }

    private void Update(DatabaseRestoreJobDto value)
    {
        _jobs[value.JobId] = value;
    }
}
