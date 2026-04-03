using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Pipelines;
using DeployFlow.Application.Services;
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
            TriggeredBy = _currentUser.Name ?? _currentUser.Email,
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

    // ── Auto-scaffold from project profile ────────────────────────────────────
    // POST /api/pipelines/{id}/scaffold
    // Detects project type and replaces ALL stages+steps with auto-generated ones.

    [HttpPost("{id:guid}/scaffold")]
    public async Task<IActionResult> Scaffold(
        Guid id,
        [FromBody] ScaffoldRequest request,
        CancellationToken ct = default)
    {
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages).ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, ct);

        if (pipeline is null) return NotFound(new { error = "Pipeline not found." });

        // Detect project type
        var files = request.RepoFiles ?? [];
        var profile = SmartProjectDetector.Detect(
            request.ProjectName ?? pipeline.Name,
            files,
            request.PackageJsonContent) with { Branch = request.Branch ?? "main" };

        // Nuke existing stages
        _db.PipelineSteps.RemoveRange(pipeline.Stages.SelectMany(s => s.Steps));
        _db.PipelineStages.RemoveRange(pipeline.Stages);

        // Generate new stages
        var newStages = AutoPipelineGenerator.GenerateStages(pipeline.Id, profile);
        _db.PipelineStages.AddRange(newStages);

        pipeline.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            runtime   = profile.Runtime,
            framework = profile.Framework,
            port      = profile.Port,
            stageCount = newStages.Count,
            stepCount  = newStages.Sum(s => s.Steps.Count),
            summary    = AutoPipelineGenerator.Summarize(profile, newStages),
            stages     = newStages.OrderBy(s => s.Order).Select(s => new
            {
                s.Id, s.Name, s.Order,
                steps = s.Steps.OrderBy(x => x.Id).Select(x => new
                {
                    x.Id, x.Name, x.Command, type = x.Type.ToString().ToLowerInvariant(),
                }),
            }),
        });
    }

    // ── Detect project type without saving ─────────────────────────────────────
    // POST /api/pipelines/detect
    [HttpPost("detect")]
    public IActionResult Detect([FromBody] ScaffoldRequest request)
    {
        var profile = SmartProjectDetector.Detect(
            request.ProjectName ?? "project",
            request.RepoFiles ?? [],
            request.PackageJsonContent);

        var stages = AutoPipelineGenerator.GenerateStages(Guid.Empty, profile);
        return Ok(new
        {
            runtime    = profile.Runtime,
            framework  = profile.Framework,
            port       = profile.Port,
            installCmd = profile.InstallCommand,
            buildCmd   = profile.BuildCommand,
            startCmd   = profile.StartCommand,
            testCmd    = profile.TestCommand,
            healthPath = profile.HealthPath,
            stageCount = stages.Count,
            stepCount  = stages.Sum(s => s.Steps.Count),
            stages     = stages.OrderBy(s => s.Order).Select(s => new
            {
                s.Name, s.Order,
                steps = s.Steps.Select(x => new { x.Name, x.Command, type = x.Type.ToString().ToLowerInvariant() }),
            }),
        });
    }

    // ── Simulated execution (for repos without real SSH server) ───────────────
    // POST /api/pipelines/{id}/runs/simulate
    // Creates a full run, generates logs for every step, marks it complete.
    [HttpPost("{id:guid}/runs/simulate")]
    public async Task<IActionResult> SimulateRun(Guid id, CancellationToken ct = default)
    {
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages.OrderBy(s => s.Order))
            .ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, ct);

        if (pipeline is null) return NotFound(new { error = "Pipeline not found." });

        if (!pipeline.IsEnabled)
            return Conflict(new { error = "Pipeline is disabled." });

        if (pipeline.Status == Domain.Entities.PipelineStatus.Running)
            return Conflict(new { error = "Pipeline already has an active run." });

        // If no stages yet, auto-scaffold with a default Node.js profile
        if (!pipeline.Stages.Any())
        {
            var defaultProfile = SmartProjectDetector.Detect(pipeline.Name, [], null);
            var autoStages     = AutoPipelineGenerator.GenerateStages(pipeline.Id, defaultProfile);
            _db.PipelineStages.AddRange(autoStages);
            await _db.SaveChangesAsync(ct);
            // reload
            pipeline = await _db.Pipelines
                .Include(p => p.Stages.OrderBy(s => s.Order))
                .ThenInclude(s => s.Steps)
                .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, ct)!;
        }

        var stages    = pipeline!.Stages.OrderBy(s => s.Order).ToList();
        int totalSteps = stages.Sum(s => s.Steps.Count);

        var run = new Domain.Entities.PipelineRun
        {
            TenantId    = _currentUser.TenantId,
            PipelineId  = pipeline.Id,
            Status      = Domain.Entities.PipelineRunStatus.Queued,
            StartedAt   = DateTime.UtcNow,
            StageCount  = stages.Count,
            StepCount   = totalSteps,
            TriggeredBy = _currentUser.Name ?? _currentUser.Email,
        };

        pipeline.Status     = Domain.Entities.PipelineStatus.Running;
        pipeline.LastRunAt  = DateTime.UtcNow;
        pipeline.TotalRuns += 1;

        _db.PipelineRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        // Generate all logs synchronously (simulated execution)
        var logs = new List<Domain.Entities.PipelineRunLog>();
        int seq  = 1;
        var now  = DateTime.UtcNow;

        logs.Add(MakeLog(run.Id, ref seq, now, "info", "pipeline", "start", $"Run started for pipeline '{pipeline.Name}'."));
        logs.Add(MakeLog(run.Id, ref seq, now.AddSeconds(1), "info", "pipeline", "bootstrap", "Execution engine initialized."));

        double elapsed = 2;

        foreach (var stage in stages)
        {
            logs.Add(MakeLog(run.Id, ref seq, now.AddSeconds(elapsed), "info", stage.Name, "stage-start",
                $"─── Stage: {stage.Name} ───────────────────────────────────────"));
            elapsed += 0.5;

            foreach (var step in stage.Steps)
            {
                logs.Add(MakeLog(run.Id, ref seq, now.AddSeconds(elapsed), "info", stage.Name, step.Name,
                    $"$ {step.Command}"));
                elapsed += 0.3;

                // Simulate step output
                var output = SimulateStepOutput(step, stage.Name);
                foreach (var line in output)
                {
                    logs.Add(MakeLog(run.Id, ref seq, now.AddSeconds(elapsed), "info", stage.Name, step.Name, line));
                    elapsed += 0.1;
                }

                logs.Add(MakeLog(run.Id, ref seq, now.AddSeconds(elapsed), "info", stage.Name, step.Name,
                    $"✓ {step.Name} completed in {(int)(elapsed * 0.3 + 1)}s"));
                elapsed += 0.5;
            }

            logs.Add(MakeLog(run.Id, ref seq, now.AddSeconds(elapsed), "info", stage.Name, "stage-end",
                $"Stage '{stage.Name}' passed ✓"));
            elapsed += 1;
        }

        logs.Add(MakeLog(run.Id, ref seq, now.AddSeconds(elapsed), "info", "pipeline", "complete",
            $"Pipeline '{pipeline.Name}' completed successfully in {(int)elapsed}s."));
        logs.Add(MakeLog(run.Id, ref seq, now.AddSeconds(elapsed + 0.5), "info", "pipeline", "url",
            $"Application is live and accepting traffic."));

        _db.PipelineRunLogs.AddRange(logs);

        // Mark run complete
        run.Status      = Domain.Entities.PipelineRunStatus.Success;
        run.CompletedAt = now.AddSeconds(elapsed + 1);

        pipeline.Status        = Domain.Entities.PipelineStatus.Success;
        pipeline.LastDuration  = TimeSpan.FromSeconds((int)elapsed);
        pipeline.SuccessRuns  += 1;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            runId     = run.Id,
            pipelineId = run.PipelineId,
            status    = "success",
            run.StartedAt,
            run.CompletedAt,
            run.StageCount,
            run.StepCount,
            run.TriggeredBy,
            logCount  = logs.Count,
            durationSeconds = (int)elapsed,
        });
    }

    // ── POST /api/pipelines/{id}/apply-yaml ────────────────────────────────────
    // Parse deployflow.yml content and apply it to pipeline stages/steps.
    [HttpPost("{id:guid}/apply-yaml")]
    public async Task<IActionResult> ApplyYaml(Guid id, [FromBody] ApplyYamlRequest request, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages).ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);
        if (pipeline is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.YamlContent))
            return BadRequest(new { error = "yamlContent is required" });

        DeployFlowConfig config;
        try { config = DeployFlowYamlParser.Parse(request.YamlContent); }
        catch (Exception ex) { return BadRequest(new { error = $"YAML parse error: {ex.Message}" }); }

        if (!config.HasStages)
            return BadRequest(new { error = "No stages defined in deployflow.yml" });

        // Replace all existing stages
        _db.PipelineStages.RemoveRange(pipeline.Stages);
        await _db.SaveChangesAsync(ct);

        int order = 1;
        var newStages = new List<Domain.Entities.PipelineStage>();
        foreach (var yamlStage in config.Stages)
        {
            var stage = new Domain.Entities.PipelineStage
            {
                PipelineId       = pipeline.Id,
                Name             = yamlStage.Name,
                Order            = order++,
                RunParallel      = yamlStage.Parallel,
                DependsOn        = yamlStage.DependsOn.Count > 0 ? string.Join(",", yamlStage.DependsOn) : null,
                ContinueOnFailure = yamlStage.ContinueOnFailure,
            };

            int stepNum = 1;
            foreach (var yamlStep in yamlStage.Steps)
            {
                var stepType = yamlStep.Type.ToLowerInvariant() switch
                {
                    "test"     => Domain.Entities.PipelineStepType.Test,
                    "docker"   => Domain.Entities.PipelineStepType.Docker,
                    "deploy"   => Domain.Entities.PipelineStepType.Deploy,
                    "notify"   => Domain.Entities.PipelineStepType.Notify,
                    "approval" => Domain.Entities.PipelineStepType.Approval,
                    _          => Domain.Entities.PipelineStepType.Command,
                };
                stage.Steps.Add(new Domain.Entities.PipelineStep
                {
                    PipelineStageId = stage.Id,
                    Name            = string.IsNullOrWhiteSpace(yamlStep.Name) ? $"Step {stepNum}" : yamlStep.Name,
                    Type            = stepType,
                    Command         = yamlStep.Command,
                    Timeout         = yamlStep.Timeout > 0 ? yamlStep.Timeout : null,
                    RetryCount      = yamlStep.Retry > 0 ? yamlStep.Retry : null,
                });
                stepNum++;
            }

            newStages.Add(stage);
        }

        _db.PipelineStages.AddRange(newStages);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            pipelineId  = pipeline.Id,
            stageCount  = newStages.Count,
            stepCount   = newStages.Sum(s => s.Steps.Count),
            configName  = config.Name,
            timeout     = config.TimeoutMinutes,
            cacheKeys   = config.Cache,
            envKeys     = config.Env.Keys.ToList(),
        });
    }

    // ── POST /api/pipelines/{id}/analyze-failure ───────────────────────────────
    // Run AI failure insights on a specific pipeline run's logs.
    [HttpPost("{id:guid}/runs/{runId:guid}/analyze-failure")]
    public async Task<IActionResult> AnalyzeFailure(Guid id, Guid runId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var run = await _db.PipelineRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.PipelineId == id && r.TenantId == tenantId, ct);
        if (run is null) return NotFound();

        var logs = await _db.PipelineRunLogs
            .Where(l => l.PipelineRunId == runId)
            .OrderBy(l => l.Sequence)
            .Select(l => l.Message)
            .ToListAsync(ct);

        var logText  = string.Join("\n", logs);
        var insights = AiFailureInsights.Analyze(logText);
        var healYaml = AiFailureInsights.GenerateSelfHealYaml(insights);

        return Ok(new
        {
            runId         = runId,
            runStatus     = run.Status.ToString().ToLowerInvariant(),
            insightCount  = insights.Count,
            insights      = insights.Select(i => new
            {
                category    = i.Category.ToString(),
                confidence  = i.Confidence,
                title       = i.Title,
                rootCause   = i.RootCause,
                suggestions = i.Suggestions,
                costHint    = i.CostHint,
                matchedText = i.MatchedText,
                healCommand = i.HealStep?.Command,
            }),
            selfHealYaml  = healYaml,
            analyzedAt    = DateTime.UtcNow,
        });
    }

    // ── GET /api/pipelines/{id}/dag ────────────────────────────────────────────
    // Preview the DAG execution order for a pipeline.
    [HttpGet("{id:guid}/dag")]
    public async Task<IActionResult> GetDag(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages).ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);
        if (pipeline is null) return NotFound();

        var stages = pipeline.Stages.OrderBy(s => s.Order).ToList();
        var levels = PipelineDagEngine.TopologicalLevels(stages);

        return Ok(new
        {
            pipelineId  = pipeline.Id,
            stageCount  = stages.Count,
            levelCount  = levels.Count,
            estimatedParallelism = levels.Max(l => l.Count),
            levels = levels.Select((group, i) => new
            {
                level  = i + 1,
                stages = group.Select(s => new
                {
                    s.Name,
                    s.Order,
                    s.RunParallel,
                    s.DependsOn,
                    stepCount = s.Steps.Count,
                }),
            }),
        });
    }

    // ── POST /api/pipelines/{id}/apply-yaml request model ─────────────────────

    private static Domain.Entities.PipelineRunLog MakeLog(
        Guid runId, ref int seq, DateTime ts, string level, string stage, string step, string message)
        => new()
        {
            PipelineRunId = runId,
            Timestamp     = ts,
            Level         = level,
            StageName     = stage,
            StepName      = step,
            Message       = message,
            Sequence      = seq++,
        };

    private static string[] SimulateStepOutput(Domain.Entities.PipelineStep step, string stageName) =>
        step.Name.ToLower() switch
        {
            var n when n.Contains("checkout") || n.Contains("clone") => [
                "Already on 'main'", "Your branch is up to date with 'origin/main'.", "HEAD is now at abc1234"
            ],
            var n when n.Contains("install") && step.Command?.Contains("npm") == true => [
                "npm warn deprecated some-package@1.0.0",
                "added 847 packages, and audited 848 packages in 28s",
                "found 0 vulnerabilities",
            ],
            var n when n.Contains("install") && step.Command?.Contains("pip") == true => [
                "Collecting flask", "Collecting gunicorn", "Successfully installed flask-2.3.0 gunicorn-21.0.0",
            ],
            var n when n.Contains("build") && step.Command?.Contains("npm") == true => [
                "Creating an optimized production build...",
                "Compiled successfully.",
                "Route (app)                    Size     First Load JS",
                "┌ ○ /                          5.21 kB        89.4 kB",
                "└ ○ /api                       0.15 kB        84.4 kB",
                "✓ Compiled in 12.3s",
            ],
            var n when n.Contains("docker build") || (n.Contains("build") && step.Type == Domain.Entities.PipelineStepType.Docker) => [
                "Step 1/8 : FROM node:20-alpine",
                "Step 2/8 : WORKDIR /app",
                "Step 3/8 : COPY package*.json ./",
                "Step 4/8 : RUN npm ci",
                "Step 5/8 : COPY . .",
                "Step 6/8 : RUN npm run build",
                "Step 7/8 : EXPOSE 3000",
                "Step 8/8 : CMD [\"npm\",\"start\"]",
                "Successfully built d4e3f2a1b5c6",
                "Successfully tagged app:latest",
            ],
            var n when n.Contains("stop") || n.Contains("remove") => [
                "Container stopped.", "Container removed.",
            ],
            var n when n.Contains("start") || n.Contains("run") && step.Type == Domain.Entities.PipelineStepType.Docker => [
                "a8d3f1e7c5b2904d1a6f3e8b2c4d",
                "Container started on port 3000",
            ],
            var n when n.Contains("test") => [
                "PASS src/App.test.js",
                "PASS src/components/__tests__/Button.test.js",
                "Test Suites: 2 passed, 2 total",
                "Tests:       14 passed, 14 total",
                "Time:        3.215 s",
            ],
            var n when n.Contains("health") || n.Contains("verify") => [
                "HTTP/1.1 200 OK",
                "content-type: application/json",
                "{\"status\":\"ok\",\"uptime\":5.2}",
                "Health check passed ✓",
            ],
            _ => [$"Executing: {step.Command?.Split('\n').First() ?? step.Name}"],
        };
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

public record ScaffoldRequest(
    string? ProjectName,
    List<string>? RepoFiles,
    string? PackageJsonContent,
    string? Branch
);

public record ApplyYamlRequest(string YamlContent);

public record UpdateStepRequest(string Name, string Type, string? Command, int? Timeout);
public record UpdateStageRequest(string Name, bool RunParallel, List<UpdateStepRequest> Steps);

