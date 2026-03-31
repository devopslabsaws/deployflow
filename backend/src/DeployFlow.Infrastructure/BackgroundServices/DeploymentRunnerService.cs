using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.Persistence;
using DeployFlow.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DeployFlow.Infrastructure.BackgroundServices;

/// <summary>
/// Polls for queued deployments and orchestrates the full build + deploy pipeline.
/// Delegates the actual script generation, execution, retry logic, and smart-fixing
/// to <see cref="IBuildService"/> â€” keeping this class focused purely on the
/// orchestration lifecycle (state transitions, notifications, watchdog).
/// </summary>
public class DeploymentRunnerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DeploymentRunnerService> _logger;
    private readonly IDeploymentLogBroadcaster _broadcaster;
    private static readonly ConcurrentDictionary<Guid, bool> _runningDeployments = new();

    private static readonly TimeSpan StuckDeploymentTimeout = TimeSpan.FromMinutes(120); // 2-hour watchdog

    // Tracks when each deployment was picked up so the watchdog can detect hangs
    private static readonly ConcurrentDictionary<Guid, DateTime> _deploymentStartTimes = new();

    // ── Observable runner state (used by /api/runner/status) ──────────────────
    public static DateTime LastPollUtc { get; private set; } = DateTime.MinValue;
    public static DateTime StartedAtUtc { get; private set; } = DateTime.MinValue;
    public static int TotalPollCycles { get; private set; }
    public static int TotalDeploymentsProcessed { get; private set; }
    public static int ActiveDeploymentCount => _runningDeployments.Count;

    // Heartbeat: log every 60 polls (= every 5 min at 5s intervals)
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(5);
    private DateTime _lastHeartbeat = DateTime.MinValue;

    public DeploymentRunnerService(
        IServiceProvider services,
        ILogger<DeploymentRunnerService> logger,
        IDeploymentLogBroadcaster broadcaster)
    {
        _services = services;
        _logger = logger;
        _broadcaster = broadcaster;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    //  Background loop
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StartedAtUtc = DateTime.UtcNow;
        _logger.LogInformation("DeploymentRunner started at {Time:u}. Polling every 5 s.", StartedAtUtc);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                LastPollUtc = DateTime.UtcNow;
                TotalPollCycles++;

                // Periodic heartbeat so we know the runner is alive even when idle
                if (DateTime.UtcNow - _lastHeartbeat > HeartbeatInterval)
                {
                    _logger.LogInformation(
                        "DeploymentRunner heartbeat — cycles: {Cycles}, active: {Active}, processed: {Done}, uptime: {Up:F0}m",
                        TotalPollCycles, _runningDeployments.Count, TotalDeploymentsProcessed,
                        (DateTime.UtcNow - StartedAtUtc).TotalMinutes);
                    _lastHeartbeat = DateTime.UtcNow;
                }

                await ProcessQueuedDeploymentsAsync(stoppingToken);
                await FailStuckDeploymentsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeploymentRunner loop");
                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ProcessQueuedDeploymentsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        List<Deployment> queued;
        try
        {
            var active = await uow.Deployments.GetActiveDeploymentsAsync(ct);
            queued = active.Where(d => d.Status == DeploymentStatus.Queued).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeploymentRunner: DB error fetching queued deployments — is Oracle reachable?");
            return;
        }

        if (queued.Count == 0) return; // silent — no noise when idle

        _logger.LogInformation("DeploymentRunner: found {Count} queued deployment(s).", queued.Count);

        foreach (var deployment in queued)
        {
            if (_runningDeployments.ContainsKey(deployment.Id))
            {
                _logger.LogDebug("DeploymentRunner: {Id} already in-flight, skipping.", deployment.Id);
                continue;
            }

            if (_runningDeployments.TryAdd(deployment.Id, true))
            {
                _deploymentStartTimes[deployment.Id] = DateTime.UtcNow;
                TotalDeploymentsProcessed++;

                _logger.LogInformation(
                    "DeploymentRunner: picked up deployment {Id} (project {ProjectId}) at {Time:u}.",
                    deployment.Id, deployment.ProjectId, DateTime.UtcNow);

                // Broadcast immediately so the UI shows the runner is alive
                _ = _broadcaster.BroadcastLogAsync(deployment.Id,
                    $"[{DateTime.UtcNow:HH:mm:ss}] ✅ Runner picked up deployment — starting pipeline...",
                    "stdout");

                var runTask = Task.Run(() => RunDeploymentAsync(deployment.Id, ct));

                // Safety net: if the outer task itself is cancelled before the
                // body runs, unblock the Id so the next poll can retry it.
                _ = runTask.ContinueWith(
                    t => _runningDeployments.TryRemove(deployment.Id, out _),
                    TaskContinuationOptions.OnlyOnCanceled);
            }
        }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    //  Main deployment orchestrator
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task RunDeploymentAsync(Guid deploymentId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var uow        = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var db         = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notif      = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var blueGreen  = scope.ServiceProvider.GetRequiredService<BlueGreenDeploymentService>();

        Deployment? deployment = null;
        try
        {
            deployment = await uow.Deployments.GetByIdAsync(deploymentId, ct);
            if (deployment is null)
            {
                _logger.LogWarning("RunDeploymentAsync: deployment {Id} not found in DB.", deploymentId);
                return;
            }

            var project = await uow.Projects.GetByIdAsync(deployment.ProjectId, ct);
            if (project is null)
            {
                _logger.LogWarning("RunDeploymentAsync: project {ProjectId} not found for deployment {Id}.", deployment.ProjectId, deploymentId);
                return;
            }

            _logger.LogInformation("Starting deployment {Id} for project '{Project}' at {Time:u}",
                deploymentId, project.Name, DateTime.UtcNow);

            // Broadcast early so UI sees activity BEFORE the first DB write
            await _broadcaster.BroadcastLogAsync(deploymentId,
                $"[{DateTime.UtcNow:HH:mm:ss}] \ud83d\ude80 Runner picked up deployment for '{project.Name}' (branch: {project.RepositoryBranch ?? "main"})", null, ct);
            await _broadcaster.BroadcastLogAsync(deploymentId,
                $"[{DateTime.UtcNow:HH:mm:ss}] \u23f3 Transitioning Queued \u2192 Building...", null, ct);

            deployment.Start();
            await uow.SaveChangesAsync(ct);
            await _broadcaster.BroadcastStatusAsync(deploymentId, "building", ct);
            await notif.NotifyDeploymentStarted(deployment.TenantId, project.Id, deploymentId, project.Name, ct);

            await _broadcaster.BroadcastLogAsync(deploymentId,
                $"[{DateTime.UtcNow:HH:mm:ss}] \u2705 Status: Building", null, ct);

            // â”€â”€ Resolve server & SSH key â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            await _broadcaster.BroadcastLogAsync(deploymentId,
                $"[{DateTime.UtcNow:HH:mm:ss}] \ud83d\udd0e Resolving server and SSH key...", null, ct);

            var server = project.ServerId.HasValue
                ? await uow.Servers.GetByIdAsync(project.ServerId.Value, ct) : null;
            if (server is null)
            {
                await FailDeploymentAsync(deployment, project,
                    "\u274c No server assigned to this project. Assign a server in Project \u2192 Settings.",
                    uow, _broadcaster, notif, ct);
                return;
            }

            await _broadcaster.BroadcastLogAsync(deploymentId,
                $"[{DateTime.UtcNow:HH:mm:ss}] \ud83d\udda5\ufe0f Target server: {server.Hostname}:{server.SshPort} ({server.Name})", null, ct);

            var sshKey = server.SshKeyId.HasValue
                ? await uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct) : null;
            if (sshKey is null)
            {
                await FailDeploymentAsync(deployment, project,
                    "\u274c No SSH key configured on server. Add an SSH key in Infrastructure \u2192 Servers.",
                    uow, _broadcaster, notif, ct);
                return;
            }

            await _broadcaster.BroadcastLogAsync(deploymentId,
                $"[{DateTime.UtcNow:HH:mm:ss}] \ud83d\udd11 SSH key resolved: {sshKey.Name}", null, ct);

            var privateKey = encryption.Decrypt(sshKey.PrivateKeyEncrypted);

            // ── Resolve env vars ──────────────────────────────────────────────────
            var envEntries = await uow.EnvVariables.GetByProjectAsync(project.Id, ct);

            await _broadcaster.BroadcastLogAsync(deploymentId,
                $"[{DateTime.UtcNow:HH:mm:ss}] \ud83d\udce6 Env vars: {envEntries.Count()} variable(s)", null, ct);

            // ── Blue/Green shortcut ────────────────────────────────────────────────
            if (deployment.Metadata.TryGetValue("strategy", out var strategy) && strategy == "blue-green")
            {
                deployment.SetDeploying();
                await uow.SaveChangesAsync(ct);
                await _broadcaster.BroadcastStatusAsync(deploymentId, "deploying", ct);

                var success = await blueGreen.DeployAsync(project, deployment, db, ct);
                if (!success)
                {
                    await FailDeploymentAsync(deployment, project, "Blue/Green deployment failed.", uow, _broadcaster, notif, ct);
                    return;
                }

                var bgUrl = project.CustomDomain is not null ? $"https://{project.CustomDomain}" : null;
                deployment.MarkSucceeded(bgUrl);
                project.RecordDeployment(deployment.Id, DeploymentStatus.Healthy);
                await uow.SaveChangesAsync(ct);
                await _broadcaster.BroadcastStatusAsync(deploymentId, "healthy", ct);
                await AddLogAsync(db, _broadcaster, deployment.Id, "\u2705 Blue/Green deployment succeeded!", ct: ct);
                await notif.NotifyDeploymentSucceeded(deployment.TenantId, project.Id, deploymentId, project.Name, bgUrl, ct);
                _logger.LogInformation("Blue/Green deployment {Id} succeeded", deploymentId);
                return;
            }

            await AddLogAsync(db, _broadcaster, deployment.Id,
                "\ud83d\udd27 Pipeline: clone \u2192 detect stack \u2192 build \u2192 dockerize \u2192 run \u2192 health-check", ct: ct);

            deployment.SetDeploying();
            await uow.SaveChangesAsync(ct);
            await _broadcaster.BroadcastStatusAsync(deploymentId, "deploying", ct);

            // â”€â”€ Delegate build + deploy to IBuildService â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var buildService = scope.ServiceProvider.GetRequiredService<IBuildService>();

            var buildRequest = new BuildRequest(deployment, project, server, privateKey, envEntries);

            var buildResult = await buildService.BuildAndDeployAsync(
                buildRequest,
                async (message, stream) => await AddLogAsync(db, _broadcaster, deployment.Id, message, stream, ct),
                ct);

            if (!buildResult.Success)
            {
                await FailDeploymentAsync(deployment, project,
                    buildResult.ErrorMessage ?? "Build failed.",
                    uow, _broadcaster, notif, ct);
                return;
            }

            // â”€â”€ Resolve public URL â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var publicUrl = buildResult.PublicUrl
                         ?? (project.CustomDomain is not null ? $"https://{project.CustomDomain}" : null);

            // â”€â”€ Mark success â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            deployment.MarkSucceeded(publicUrl);
            project.RecordDeployment(deployment.Id, DeploymentStatus.Healthy);
            await uow.SaveChangesAsync(ct);
            await _broadcaster.BroadcastStatusAsync(deploymentId, "healthy", ct);

            await AddLogAsync(db, _broadcaster, deployment.Id,
                publicUrl is not null
                    ? $"âœ… Deployment succeeded!\nðŸŒ App running at: {publicUrl}"
                    : "âœ… Deployment succeeded!", ct: ct);

            await notif.NotifyDeploymentSucceeded(
                deployment.TenantId, project.Id, deploymentId, project.Name, publicUrl, ct);

            _logger.LogInformation("Deployment {Id} succeeded. URL={Url}", deploymentId, publicUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in deployment {Id}", deploymentId);
            if (deployment is not null)
            {
                using var errScope = _services.CreateScope();
                var errUow   = errScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var errNotif = errScope.ServiceProvider.GetRequiredService<INotificationService>();
                var errDep   = await errUow.Deployments.GetByIdAsync(deploymentId, ct);
                if (errDep is not null)
                {
                    var errProject = await errUow.Projects.GetByIdAsync(errDep.ProjectId, ct);
                    await FailDeploymentAsync(errDep, errProject, ex.Message, errUow, _broadcaster, errNotif, ct);
                }
            }
        }
        finally
        {
            _runningDeployments.TryRemove(deploymentId, out _);
            _deploymentStartTimes.TryRemove(deploymentId, out _);
        }
    }

    /// <summary>
    /// Watchdog: marks deployments that have been running for longer than
    /// <see cref="StuckDeploymentTimeout"/> as failed.  This catches cases where
    /// the SSH thread hangs indefinitely (e.g. unreachable server, no TCP timeout).
    /// </summary>
    private async Task FailStuckDeploymentsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var (deploymentId, startedAt) in _deploymentStartTimes)
        {
            if (now - startedAt < StuckDeploymentTimeout) continue;

            _logger.LogWarning("Deployment {Id} has been running for {Hours:F1}h â€” marking as failed (watchdog).",
                deploymentId, (now - startedAt).TotalHours);

            _runningDeployments.TryRemove(deploymentId, out _);
            _deploymentStartTimes.TryRemove(deploymentId, out _);

            try
            {
                using var scope = _services.CreateScope();
                var uow   = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var dep = await uow.Deployments.GetByIdAsync(deploymentId, ct);
                if (dep is null || dep.Status is DeploymentStatus.Failed or DeploymentStatus.Cancelled
                    or DeploymentStatus.Healthy or DeploymentStatus.Running) continue;

                var project = await uow.Projects.GetByIdAsync(dep.ProjectId, ct);
                await FailDeploymentAsync(dep, project,
                    $"Deployment timed out after {StuckDeploymentTimeout.TotalMinutes:0} minutes (watchdog).",
                    uow, _broadcaster, notif, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watchdog: error failing stuck deployment {Id}", deploymentId);
            }
        }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    //  Helpers
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static async Task FailDeploymentAsync(
        Deployment deployment, Project? project, string reason,
        IUnitOfWork uow, IDeploymentLogBroadcaster broadcaster,
        INotificationService notification, CancellationToken ct)
    {
        deployment.MarkFailed(reason);
        project?.RecordDeployment(deployment.Id, DeploymentStatus.Failed);
        await uow.SaveChangesAsync(ct);
        await broadcaster.BroadcastStatusAsync(deployment.Id, "failed", ct);
        if (project is not null)
            await notification.NotifyDeploymentFailed(
                deployment.TenantId, project.Id, deployment.Id, project.Name, reason, ct);
    }

    private static async Task AddLogAsync(
        ApplicationDbContext db, IDeploymentLogBroadcaster broadcaster,
        Guid deploymentId, string message, string? stream = null, CancellationToken ct = default)
    {
        try
        {
            db.DeploymentLogs.Add(new DeploymentLog
            {
                DeploymentId = deploymentId,
                Message      = message,
                Level        = Domain.Entities.LogLevel.Info,
                Stream       = stream,
                Timestamp    = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            await broadcaster.BroadcastLogAsync(deploymentId, message, stream, ct);
        }
        catch { /* non-critical */ }
    }

    private static string TrimLog(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "\nâ€¦ (truncated)";
}
