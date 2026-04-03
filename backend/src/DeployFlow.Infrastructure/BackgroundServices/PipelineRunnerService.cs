using DeployFlow.Application.Common;
using DeployFlow.Application.Features.Deployments.Commands;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeployFlow.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that polls for queued <see cref="PipelineRun"/> records and executes them.
///
/// Execution model:
///   • Poll every 10 seconds for <see cref="PipelineRunStatus.Queued"/> runs.
///   • Each run executes stages in ascending <see cref="PipelineStage.Order"/>.
///   • Steps within a stage run in parallel when <see cref="PipelineStage.RunParallel"/> is true,
///     or sequentially otherwise.
///   • Step types:
///       Command / Test  – run shell command on project's server via SSH
///       Docker          – run docker command on server via SSH
///       Deploy          – trigger a full deployment via <see cref="TriggerDeploymentCommand"/>
///       Notify          – call <see cref="INotificationService.SendCustomAsync"/>
///       Approval        – wait up to <see cref="PipelineStep.Timeout"/> seconds for external approval
///   • On any stage failure remaining stages are marked Skipped and the run is Failed.
///   • Updates <see cref="Pipeline.TotalRuns"/>, <see cref="Pipeline.SuccessRuns"/>, and
///     <see cref="Pipeline.FailedRuns"/> on completion.
/// </summary>
public class PipelineRunnerService : BackgroundService
{
    private readonly IServiceProvider                    _services;
    private readonly ILogger<PipelineRunnerService>     _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    public PipelineRunnerService(IServiceProvider services, ILogger<PipelineRunnerService> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PipelineRunner started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedRunsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PipelineRunner: unhandled error in poll loop");
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ── Poll loop ─────────────────────────────────────────────────────────────

    private async Task ProcessQueuedRunsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var queued = await db.PipelineRuns
            .Where(r => r.Status == PipelineRunStatus.Queued)
            .OrderBy(r => r.StartedAt)
            .Take(5)   // at most 5 concurrent pipelines
            .ToListAsync(ct);

        foreach (var run in queued)
        {
            // Fire-and-forget per run so multiple can proceed in parallel
            _ = Task.Run(() => ExecuteRunAsync(run.Id, ct), ct);
        }
    }

    // ── Run executor ──────────────────────────────────────────────────────────

    private async Task ExecuteRunAsync(Guid runId, CancellationToken ct)
    {
        _logger.LogInformation("PipelineRunner: starting run {RunId}", runId);

        using var scope = _services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ssh   = scope.ServiceProvider.GetRequiredService<ISshService>();
        var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var enc   = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var uow   = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var run = await db.PipelineRuns.FindAsync(new object[] { runId }, ct);
        if (run is null || run.Status != PipelineRunStatus.Queued) return;

        // Load pipeline with stages and steps
        var pipeline = await db.Pipelines
            .Include(p => p.Stages)
                .ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(p => p.Id == run.PipelineId, ct);

        if (pipeline is null)
        {
            await MarkRunFailed(db, run, "Pipeline not found", ct);
            return;
        }

        // Mark run as running
        run.Status = PipelineRunStatus.Running;
        await db.SaveChangesAsync(ct);

        // Resolve server + SSH key for command execution
        var project = await uow.Projects.GetByIdAsync(pipeline.ProjectId, ct);
        Server? server = null;
        string? privateKey = null;

        if (project?.ServerId.HasValue == true)
        {
            server = await uow.Servers.GetByIdAsync(project.ServerId.Value, ct);
            if (server?.SshKeyId.HasValue == true)
            {
                var sshKey = await uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct);
                if (sshKey is not null)
                    privateKey = enc.Decrypt(sshKey.PrivateKeyEncrypted);
            }
        }

        var runStarted = DateTime.UtcNow;
        var logSeq = new int[] { 0 };  // int[] so async methods can safely increment it

        bool runSucceeded = true;
        string? failReason = null;

        var stages = pipeline.Stages.OrderBy(s => s.Order).ToList();

        foreach (var stage in stages)
        {
            if (!runSucceeded)
            {
                // Skip all remaining stages
                await LogAsync(db, run.Id, stage.Name, null, "⏭️ Stage skipped (prior failure)", ++logSeq[0], ct);
                continue;
            }

            await LogAsync(db, run.Id, stage.Name, null, $"▶️  Stage: {stage.Name}", ++logSeq[0], ct);

            try
            {
                bool stageSuccess;
                if (stage.RunParallel)
                    stageSuccess = await ExecuteStepsParallelAsync(stage, db, ssh, sender, notif, server, privateKey, run.Id, logSeq, ct);
                else
                    stageSuccess = await ExecuteStepsSequentialAsync(stage, db, ssh, sender, notif, server, privateKey, run.Id, logSeq, ct);

                if (!stageSuccess)
                {
                    runSucceeded = false;
                    failReason   = $"Stage '{stage.Name}' failed";
                    await LogAsync(db, run.Id, stage.Name, null, $"❌ Stage '{stage.Name}' failed", ++logSeq[0], ct);
                }
                else
                {
                    await LogAsync(db, run.Id, stage.Name, null, $"✅ Stage '{stage.Name}' completed", ++logSeq[0], ct);
                }
            }
            catch (Exception ex)
            {
                runSucceeded = false;
                failReason   = ex.Message;
                await LogAsync(db, run.Id, stage.Name, null, $"💥 Stage '{stage.Name}' threw: {ex.Message}", ++logSeq[0], ct);
                _logger.LogError(ex, "PipelineRunner: stage {Stage} in run {Run} threw", stage.Name, runId);
            }
        }

        // ── Finalise run ──────────────────────────────────────────────────────
        run.Status      = runSucceeded ? PipelineRunStatus.Success : PipelineRunStatus.Failed;
        run.CompletedAt = DateTime.UtcNow;
        run.ErrorMessage = failReason;

        // ── Update pipeline counters ──────────────────────────────────────────
        pipeline.LastRunAt   = run.CompletedAt;
        pipeline.LastDuration = run.CompletedAt - runStarted;
        pipeline.TotalRuns++;
        if (runSucceeded) pipeline.SuccessRuns++;
        else              pipeline.FailedRuns++;

        await db.SaveChangesAsync(ct);

        await LogAsync(db, run.Id, "pipeline", null,
            runSucceeded
                ? $"🎉 Pipeline '{pipeline.Name}' completed successfully in {pipeline.LastDuration?.TotalSeconds:F1}s"
                : $"💔 Pipeline '{pipeline.Name}' failed: {failReason}",
            ++logSeq[0], ct);

        _logger.LogInformation("PipelineRunner: run {RunId} finished ({Status})", runId, run.Status);
    }

    // ── Step executors ────────────────────────────────────────────────────────

    private async Task<bool> ExecuteStepsSequentialAsync(
        PipelineStage       stage,
        ApplicationDbContext db,
        ISshService         ssh,
        ISender             sender,
        INotificationService notif,
        Server?             server,
        string?             privateKey,
        Guid                runId,
        int[]               logSeq,
        CancellationToken   ct)
    {
        foreach (var step in stage.Steps.OrderBy(s => s.Id))
        {
            bool ok = await ExecuteStepAsync(stage, step, db, ssh, sender, notif, server, privateKey, runId, logSeq[0], ct);
            logSeq[0]++;
            if (!ok) return false;
        }
        return true;
    }

    private async Task<bool> ExecuteStepsParallelAsync(
        PipelineStage       stage,
        ApplicationDbContext db,
        ISshService         ssh,
        ISender             sender,
        INotificationService notif,
        Server?             server,
        string?             privateKey,
        Guid                runId,
        int[]               logSeq,
        CancellationToken   ct)
    {
        var tasks = stage.Steps
            .Select(step => ExecuteStepAsync(stage, step, db, ssh, sender, notif, server, privateKey, runId, logSeq[0]++, ct))
            .ToList();

        var results = await Task.WhenAll(tasks);
        return results.All(r => r);
    }

    private async Task<bool> ExecuteStepAsync(
        PipelineStage       stage,
        PipelineStep        step,
        ApplicationDbContext db,
        ISshService         ssh,
        ISender             sender,
        INotificationService notif,
        Server?             server,
        string?             privateKey,
        Guid                runId,
        int                 seqBase,
        CancellationToken   ct)
    {
        var stepStart = DateTime.UtcNow;
        await LogAsync(db, runId, stage.Name, step.Name, $"  ⚙️  [{step.Type}] {step.Name}", seqBase, ct);

        try
        {
            bool ok = step.Type switch
            {
                PipelineStepType.Command or PipelineStepType.Test =>
                    await ExecuteSshStepAsync(step, ssh, server, privateKey, db, runId, stage.Name, seqBase, ct),

                PipelineStepType.Docker =>
                    await ExecuteDockerStepAsync(step, ssh, server, privateKey, db, runId, stage.Name, seqBase, ct),

                PipelineStepType.Deploy =>
                    await ExecuteDeployStepAsync(step, sender, db, runId, stage.Name, seqBase, ct),

                PipelineStepType.Notify =>
                    await ExecuteNotifyStepAsync(step, notif, db, runId, stage.Name, seqBase, ct),

                PipelineStepType.Approval =>
                    await ExecuteApprovalStepAsync(step, db, runId, stage.Name, seqBase, ct),

                _ => throw new InvalidOperationException($"Unknown step type: {step.Type}")
            };

            step.Status   = ok ? PipelineStageStatus.Success : PipelineStageStatus.Failed;
            step.Duration = DateTime.UtcNow - stepStart;
            await db.SaveChangesAsync(ct);
            return ok;
        }
        catch (Exception ex)
        {
            await LogAsync(db, runId, stage.Name, step.Name, $"    💥 Step threw: {ex.Message}", seqBase + 1, ct);
            step.Status   = PipelineStageStatus.Failed;
            step.Duration = DateTime.UtcNow - stepStart;
            await db.SaveChangesAsync(ct);
            return false;
        }
    }

    // ── Concrete step handlers ────────────────────────────────────────────────

    private async Task<bool> ExecuteSshStepAsync(
        PipelineStep        step,
        ISshService         ssh,
        Server?             server,
        string?             privateKey,
        ApplicationDbContext db,
        Guid                runId,
        string              stageName,
        int                 seq,
        CancellationToken   ct)
    {
        if (server is null || privateKey is null)
        {
            await LogAsync(db, runId, stageName, step.Name, "    ⚠️ No server configured — skipping SSH step", seq, ct);
            return true; // non-fatal: step is a no-op
        }

        if (string.IsNullOrWhiteSpace(step.Command))
        {
            await LogAsync(db, runId, stageName, step.Name, "    ⚠️ No command configured — skipping", seq, ct);
            return true;
        }

        var timeout = step.Timeout.HasValue ? TimeSpan.FromSeconds(step.Timeout.Value) : TimeSpan.FromMinutes(15);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var result = await ssh.ExecuteCommandAsync(
            server.IpAddress, server.SshPort, server.SshUser, privateKey, step.Command, cts.Token);

        var output = (result.StdOut + result.StdErr).Trim();
        if (!string.IsNullOrEmpty(output))
            await LogAsync(db, runId, stageName, step.Name, "    " + TrimLog(output, 2000), seq + 1, ct);

        return result.Success && result.ExitCode == 0;
    }

    private async Task<bool> ExecuteDockerStepAsync(
        PipelineStep        step,
        ISshService         ssh,
        Server?             server,
        string?             privateKey,
        ApplicationDbContext db,
        Guid                runId,
        string              stageName,
        int                 seq,
        CancellationToken   ct)
    {
        if (server is null || privateKey is null)
        {
            await LogAsync(db, runId, stageName, step.Name, "    ⚠️ No server — skipping Docker step", seq, ct);
            return true;
        }

        var cmd = string.IsNullOrWhiteSpace(step.Command)
            ? $"docker pull {step.Image ?? "hello-world"}"
            : step.Command;

        var result = await ssh.ExecuteCommandAsync(
            server.IpAddress, server.SshPort, server.SshUser, privateKey, cmd, ct);

        var output = (result.StdOut + result.StdErr).Trim();
        if (!string.IsNullOrEmpty(output))
            await LogAsync(db, runId, stageName, step.Name, "    " + TrimLog(output, 2000), seq + 1, ct);

        return result.Success && result.ExitCode == 0;
    }

    private async Task<bool> ExecuteDeployStepAsync(
        PipelineStep        step,
        ISender             sender,
        ApplicationDbContext db,
        Guid                runId,
        string              stageName,
        int                 seq,
        CancellationToken   ct)
    {
        // Extract ProjectId from ConfigJson: {"projectId":"<guid>"}
        Guid? projectId = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(step.ConfigJson ?? "{}");
            if (doc.RootElement.TryGetProperty("projectId", out var pIdProp) &&
                pIdProp.TryGetGuid(out var pid))
                projectId = pid;
        }
        catch { /* invalid JSON — projectId stays null */ }

        if (!projectId.HasValue)
        {
            await LogAsync(db, runId, stageName, step.Name, "    ⚠️ Deploy step missing projectId in ConfigJson — skipping", seq, ct);
            return true;
        }

        await LogAsync(db, runId, stageName, step.Name, $"    🚀 Triggering deployment for project {projectId}", seq, ct);

        var result = await sender.Send(
            new TriggerDeploymentCommand(projectId.Value, null, null, "pipeline"), ct);

        if (!result.IsSuccess)
            await LogAsync(db, runId, stageName, step.Name, $"    ❌ Trigger failed: {result.Error}", seq + 1, ct);

        return result.IsSuccess;
    }

    private async Task<bool> ExecuteNotifyStepAsync(
        PipelineStep        step,
        INotificationService notif,
        ApplicationDbContext db,
        Guid                runId,
        string              stageName,
        int                 seq,
        CancellationToken   ct)
    {
        var message = step.Command ?? $"Pipeline step '{step.Name}' executed.";
        await LogAsync(db, runId, stageName, step.Name, $"    📣 Notification: {message}", seq, ct);

        try
        {
            // Extract tenantId + alert name from ConfigJson if present
            using var doc = System.Text.Json.JsonDocument.Parse(step.ConfigJson ?? "{}");
            if (doc.RootElement.TryGetProperty("tenantId", out var tProp) && tProp.TryGetGuid(out var tenantId))
            {
                var alertName = step.Name;
                if (doc.RootElement.TryGetProperty("alertName", out var aProp))
                    alertName = aProp.GetString() ?? alertName;

                await notif.NotifyAlertTriggered(tenantId, alertName, "info", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PipelineRunner: notify step failed but non-fatal");
        }

        return true; // notification failures are non-fatal
    }

    private async Task<bool> ExecuteApprovalStepAsync(
        PipelineStep        step,
        ApplicationDbContext db,
        Guid                runId,
        string              stageName,
        int                 seq,
        CancellationToken   ct)
    {
        var timeout = step.Timeout.HasValue
            ? TimeSpan.FromSeconds(step.Timeout.Value)
            : TimeSpan.FromHours(24);

        await LogAsync(db, runId, stageName, step.Name,
            $"    ⏳ Awaiting approval (timeout: {timeout.TotalMinutes:F0} min)...", seq, ct);

        // Poll db for approval every 15 seconds until timeout or approved
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        while (!cts.Token.IsCancellationRequested)
        {
            var freshStep = await db.PipelineSteps.FindAsync(new object[] { step.Id }, cts.Token);
            if (freshStep?.Status == PipelineStageStatus.Success)
            {
                await LogAsync(db, runId, stageName, step.Name, "    ✅ Approval granted", seq + 1, ct);
                return true;
            }
            if (freshStep?.Status == PipelineStageStatus.Failed)
            {
                await LogAsync(db, runId, stageName, step.Name, "    ❌ Approval rejected", seq + 1, ct);
                return false;
            }

            try { await Task.Delay(TimeSpan.FromSeconds(15), cts.Token); }
            catch (OperationCanceledException) { break; }
        }

        await LogAsync(db, runId, stageName, step.Name, "    ⏱️ Approval timed out", seq + 1, ct);
        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task MarkRunFailed(ApplicationDbContext db, PipelineRun run, string reason, CancellationToken ct)
    {
        run.Status       = PipelineRunStatus.Failed;
        run.CompletedAt  = DateTime.UtcNow;
        run.ErrorMessage = reason;
        await db.SaveChangesAsync(ct);
    }

    private static async Task LogAsync(
        ApplicationDbContext db,
        Guid                 runId,
        string               stageName,
        string?              stepName,
        string               message,
        int                  seq,
        CancellationToken    ct)
    {
        try
        {
            db.PipelineRunLogs.Add(new PipelineRunLog
            {
                PipelineRunId = runId,
                StageName     = stageName,
                StepName      = stepName,
                Message       = message,
                Level         = "info",
                Sequence      = seq,
                Timestamp     = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        catch { /* logging is non-critical */ }
    }

    private static string TrimLog(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "\n… (truncated)";
}
