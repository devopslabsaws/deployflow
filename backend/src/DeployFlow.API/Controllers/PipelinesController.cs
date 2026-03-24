using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Pipelines;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/pipelines")]
public class PipelinesController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public PipelinesController(
        IMediator mediator,
        ApplicationDbContext db,
        ICurrentUser currentUser) : base(mediator)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetPipelines(
        [FromQuery] Guid? projectId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetPipelinesQuery(projectId, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetPipelineByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePipelineRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new CreatePipelineCommand(
            request.Name, request.Description, request.ProjectId,
            request.Trigger, request.CronExpression), ct));

    [HttpPost("{id:guid}/trigger")]
    public async Task<IActionResult> Trigger(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new TriggerPipelineCommand(id), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeletePipelineCommand(id), ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePipelineRequest request,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new UpdatePipelineCommand(
            id,
            request.Name,
            request.Description,
            request.Trigger,
            request.CronExpression,
            request.IsEnabled), ct));

    [HttpPost("{id:guid}/runs")]
    public async Task<IActionResult> StartRun(Guid id, CancellationToken ct = default)
    {
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages)
            .ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, ct);

        if (pipeline is null)
            return NotFound(new { error = "Pipeline not found." });

        if (!pipeline.IsEnabled)
            return Conflict(new { error = "Pipeline is disabled." });

        if (pipeline.Status == Domain.Entities.PipelineStatus.Running)
            return Conflict(new { error = "Pipeline already has an active run." });

        var run = new Domain.Entities.PipelineRun
        {
            TenantId = _currentUser.TenantId,
            PipelineId = pipeline.Id,
            Status = Domain.Entities.PipelineRunStatus.Running,
            StartedAt = DateTime.UtcNow,
            StageCount = pipeline.Stages.Count,
            StepCount = pipeline.Stages.Sum(s => s.Steps.Count),
            TriggeredBy = _currentUser.Email,
        };

        pipeline.Status = Domain.Entities.PipelineStatus.Running;
        pipeline.LastRunAt = DateTime.UtcNow;
        pipeline.TotalRuns += 1;

        _db.PipelineRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        _db.PipelineRunLogs.AddRange(
            new Domain.Entities.PipelineRunLog
            {
                PipelineRunId = run.Id,
                Timestamp = DateTime.UtcNow,
                Level = "info",
                StageName = "pipeline",
                StepName = "start",
                Message = $"Run started for pipeline '{pipeline.Name}'.",
                Sequence = 1,
            },
            new Domain.Entities.PipelineRunLog
            {
                PipelineRunId = run.Id,
                Timestamp = DateTime.UtcNow,
                Level = "info",
                StageName = pipeline.Stages.OrderBy(s => s.Order).FirstOrDefault()?.Name ?? "pipeline",
                StepName = "bootstrap",
                Message = "Execution engine initialized.",
                Sequence = 2,
            });

        await _db.SaveChangesAsync(ct);

        return Ok(new
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
        });
    }

    [HttpGet("{id:guid}/runs")]
    public async Task<IActionResult> GetRuns(Guid id, CancellationToken ct = default)
    {
        var pipelineExists = await _db.Pipelines
            .AsNoTracking()
            .AnyAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, ct);

        if (!pipelineExists)
            return NotFound(new { error = "Pipeline not found." });

        var runs = await _db.PipelineRuns
            .AsNoTracking()
            .Where(r => r.PipelineId == id && r.TenantId == _currentUser.TenantId)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new
            {
                r.Id,
                r.PipelineId,
                status = r.Status.ToString().ToLowerInvariant(),
                r.StartedAt,
                r.CompletedAt,
                r.StageCount,
                r.StepCount,
                r.TriggeredBy,
                r.ErrorMessage,
            })
            .ToListAsync(ct);

        return Ok(runs);
    }

    [HttpPut("{id:guid}/stages")]
    public async Task<IActionResult> UpdateStages(
        Guid id,
        [FromBody] List<UpdateStageRequest> stages,
        CancellationToken ct = default)
    {
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages)
            .ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, ct);

        if (pipeline is null)
            return NotFound(new { error = "Pipeline not found." });

        // Remove existing steps and stages
        _db.PipelineSteps.RemoveRange(pipeline.Stages.SelectMany(s => s.Steps));
        _db.PipelineStages.RemoveRange(pipeline.Stages);

        // Add new stages with steps
        for (int i = 0; i < stages.Count; i++)
        {
            var req = stages[i];
            var stage = new Domain.Entities.PipelineStage
            {
                PipelineId = pipeline.Id,
                Name = req.Name,
                Order = i,
                RunParallel = req.RunParallel,
            };
            for (int j = 0; j < req.Steps.Count; j++)
            {
                var sr = req.Steps[j];
                stage.Steps.Add(new Domain.Entities.PipelineStep
                {
                    Name = sr.Name,
                    Type = Enum.TryParse<Domain.Entities.PipelineStepType>(sr.Type, true, out var t)
                        ? t : Domain.Entities.PipelineStepType.Command,
                    Command = sr.Command,
                    Timeout = sr.Timeout,
                });
            }
            _db.PipelineStages.Add(stage);
        }

        pipeline.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            stageCount = stages.Count,
            stepCount = stages.Sum(s => s.Steps.Count),
        });
    }
}

public record CreatePipelineRequest(
    string Name,
    string? Description,
    Guid ProjectId,
    string Trigger,
    string? CronExpression
);

public record UpdatePipelineRequest(
    string Name,
    string? Description,
    string Trigger,
    string? CronExpression,
    bool IsEnabled
);

public record UpdateStepRequest(string Name, string Type, string? Command, int? Timeout);
public record UpdateStageRequest(string Name, bool RunParallel, List<UpdateStepRequest> Steps);
