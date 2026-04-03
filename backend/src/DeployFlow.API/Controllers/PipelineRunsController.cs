using DeployFlow.Application.Common;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/pipeline-runs")]
public class PipelineRunsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public PipelineRunsController(
        IMediator mediator,
        ApplicationDbContext db,
        ICurrentUser currentUser)
        : base(mediator)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("{runId:guid}")]
    public async Task<IActionResult> GetById(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.PipelineRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId && r.TenantId == _currentUser.TenantId, ct);

        if (run is null)
            return NotFound(new { error = "Pipeline run not found." });

        return Ok(Map(run));
    }

    [HttpGet("{runId:guid}/logs")]
    public async Task<IActionResult> GetLogs(
        Guid runId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        CancellationToken ct = default)
    {
        var runExists = await _db.PipelineRuns
            .AsNoTracking()
            .AnyAsync(r => r.Id == runId && r.TenantId == _currentUser.TenantId, ct);

        if (!runExists)
            return NotFound(new { error = "Pipeline run not found." });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 20, 1000);

        var query = _db.PipelineRunLogs
            .AsNoTracking()
            .Where(l => l.PipelineRunId == runId)
            .OrderBy(l => l.Sequence);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.Timestamp,
                l.Level,
                l.StageName,
                l.StepName,
                l.Message,
                l.Sequence
            })
            .ToListAsync(ct);

        return Ok(new
        {
            data = items,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    [HttpPost("{runId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.PipelineRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.TenantId == _currentUser.TenantId, ct);

        if (run is null)
            return NotFound(new { error = "Pipeline run not found." });

        if (run.Status is Domain.Entities.PipelineRunStatus.Success or Domain.Entities.PipelineRunStatus.Failed or Domain.Entities.PipelineRunStatus.Cancelled)
            return Conflict(new { error = "Run is already in a terminal state." });

        run.Status = Domain.Entities.PipelineRunStatus.Cancelled;
        run.CompletedAt = DateTime.UtcNow;

        var pipeline = await _db.Pipelines.FirstOrDefaultAsync(
            p => p.Id == run.PipelineId && p.TenantId == _currentUser.TenantId,
            ct);

        if (pipeline is not null)
            pipeline.Status = Domain.Entities.PipelineStatus.Cancelled;

        var nextSeq = await _db.PipelineRunLogs
            .Where(x => x.PipelineRunId == run.Id)
            .Select(x => (int?)x.Sequence)
            .MaxAsync(ct) ?? 0;

        _db.PipelineRunLogs.Add(new Domain.Entities.PipelineRunLog
        {
            PipelineRunId = run.Id,
            Timestamp = DateTime.UtcNow,
            Level = "warn",
            StageName = "pipeline",
            StepName = "cancel",
            Message = "Run cancelled by user.",
            Sequence = nextSeq + 1,
        });

        await _db.SaveChangesAsync(ct);

        return Ok(Map(run));
    }

    private static object Map(Domain.Entities.PipelineRun run) => new
    {
        run.Id,
        run.PipelineId,
        status = run.Status.ToString().ToLowerInvariant(),
        run.StartedAt,
        run.CompletedAt,
        run.StageCount,
        run.StepCount,
        run.TriggeredBy,
        run.ErrorMessage,
    };
}
