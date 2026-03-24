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
/// Polls for queued deployments and orchestrates the actual build + deploy pipeline.
/// Runs indefinitely as a hosted service, processing one deployment at a time per server.
/// </summary>
public class DeploymentRunnerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DeploymentRunnerService> _logger;
    private readonly IDeploymentLogBroadcaster _broadcaster;
    private static readonly ConcurrentDictionary<Guid, bool> _runningDeployments = new();

    public DeploymentRunnerService(
        IServiceProvider services,
        ILogger<DeploymentRunnerService> logger,
        IDeploymentLogBroadcaster broadcaster)
    {
        _services = services;
        _logger = logger;
        _broadcaster = broadcaster;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeploymentRunner started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedDeploymentsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
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

        var queued = await uow.Deployments.GetActiveDeploymentsAsync(ct);
        var queuedOnly = queued.Where(d => d.Status == DeploymentStatus.Queued).ToList();

        foreach (var deployment in queuedOnly)
        {
            if (_runningDeployments.ContainsKey(deployment.Id)) continue;
            if (_runningDeployments.TryAdd(deployment.Id, true))
            {
        _ = Task.Run(() => RunDeploymentAsync(deployment.Id, ct), ct);
            }
        }
    }

    private async Task RunDeploymentAsync(Guid deploymentId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notification = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var git = scope.ServiceProvider.GetRequiredService<IGitService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ISshService>();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var blueGreen = scope.ServiceProvider.GetRequiredService<BlueGreenDeploymentService>();

        Deployment? deployment = null;
        try
        {
            deployment = await uow.Deployments.GetByIdAsync(deploymentId, ct);
            if (deployment is null) return;

            var project = await uow.Projects.GetByIdAsync(deployment.ProjectId, ct);
            if (project is null) return;

            _logger.LogInformation("Starting deployment {Id} for project {Project}",
                deploymentId, project.Name);

            // Mark as building
            deployment.Start();
            await uow.SaveChangesAsync(ct);
            await _broadcaster.BroadcastStatusAsync(deploymentId, "building", ct);

            await notification.NotifyDeploymentStarted(
                deployment.TenantId, project.Id, deploymentId, project.Name, ct);

            // Get the server
            var server = project.ServerId.HasValue
                ? await uow.Servers.GetByIdAsync(project.ServerId.Value, ct)
                : null;

            if (server is null)
            {
                await FailDeploymentAsync(deployment, project, "No server assigned to project.", uow, _broadcaster, notification, ct);
                return;
            }

            // Get SSH key
            SshKey? sshKey = server.SshKeyId.HasValue
                ? await uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct)
                : null;

            if (sshKey is null)
            {
                await FailDeploymentAsync(deployment, project, "No SSH key configured on server.", uow, _broadcaster, notification, ct);
                return;
            }

            var privateKey = encryption.Decrypt(sshKey.PrivateKeyEncrypted);

            // Clone / pull repo
            await AddLogAsync(db, _broadcaster, deployment.Id, "📦 Fetching source code...", ct: ct);
            deployment.SetDeploying();
            await uow.SaveChangesAsync(ct);
            await _broadcaster.BroadcastStatusAsync(deploymentId, "deploying", ct);

            // Get env vars for the project
            var envVars = await uow.EnvVariables.GetByProjectAsync(project.Id, ct);
            var envDict = envVars.ToDictionary(e => e.Key, e => e.Value ?? "");

            // Build the deploy script
            var deployScript = BuildDeployScript(project, envDict);

            await AddLogAsync(db, _broadcaster, deployment.Id, "🔨 Running build commands...", ct: ct);

            // Blue/Green strategy
            if (deployment.Metadata.TryGetValue("strategy", out var strategy) && strategy == "blue-green")
            {
                var success = await blueGreen.DeployAsync(project, deployment, db, ct);
                if (!success)
                {
                    await FailDeploymentAsync(deployment, project, "Blue/Green deployment failed.", uow, _broadcaster, notification, ct);
                    return;
                }

                var blueGreenUrl = project.CustomDomain is not null ? $"https://{project.CustomDomain}" : null;
                deployment.MarkSucceeded(blueGreenUrl);
                project.RecordDeployment(deployment.Id, DeploymentStatus.Healthy);
                await uow.SaveChangesAsync(ct);
                await _broadcaster.BroadcastStatusAsync(deploymentId, "healthy", ct);
                await AddLogAsync(db, _broadcaster, deployment.Id, "✅ Blue/Green deployment succeeded!", ct: ct);
                await notification.NotifyDeploymentSucceeded(deployment.TenantId, project.Id, deploymentId, project.Name, blueGreenUrl, ct);
                _logger.LogInformation("Blue/Green deployment {Id} succeeded", deploymentId);
                return;
            }

            var result = await ssh.ExecuteCommandAsync(
                server.IpAddress, server.SshPort, server.SshUser, privateKey,
                deployScript, ct);

            if (result.ExitCode != 0 || !result.Success)
            {
                await AddLogAsync(db, _broadcaster, deployment.Id,
                    $"Deploy script failed (exit {result.ExitCode}):\n{result.StdErr}", "stderr", ct);
                await FailDeploymentAsync(deployment, project,
                    $"Deploy script failed (exit {result.ExitCode}):\n{result.StdErr}",
                    uow, _broadcaster, notification, ct);
                return;
            }

            await AddLogAsync(db, _broadcaster, deployment.Id, result.StdOut, "stdout", ct);

            // Success
            var url = project.CustomDomain is not null ? $"https://{project.CustomDomain}" : null;
            deployment.MarkSucceeded(url);
            project.RecordDeployment(deployment.Id, DeploymentStatus.Healthy);
            await uow.SaveChangesAsync(ct);
            await _broadcaster.BroadcastStatusAsync(deploymentId, "healthy", ct);
            await AddLogAsync(db, _broadcaster, deployment.Id, "✅ Deployment succeeded!", ct: ct);

            await notification.NotifyDeploymentSucceeded(
                deployment.TenantId, project.Id, deploymentId, project.Name, url, ct);

            _logger.LogInformation("Deployment {Id} succeeded", deploymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in deployment {Id}", deploymentId);
            if (deployment is not null)
            {
                using var errScope = _services.CreateScope();
                var errUow = errScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var errNotif = errScope.ServiceProvider.GetRequiredService<INotificationService>();
                var errDeployment = await errUow.Deployments.GetByIdAsync(deploymentId, ct);
                if (errDeployment is not null)
                {
                    var project = await errUow.Projects.GetByIdAsync(errDeployment.ProjectId, ct);
                    await FailDeploymentAsync(errDeployment, project, ex.Message, errUow, _broadcaster, errNotif, ct);
                }
            }
        }
        finally
        {
            _runningDeployments.TryRemove(deploymentId, out _);
        }
    }

    private static async Task FailDeploymentAsync(
        Deployment deployment,
        Project? project,
        string reason,
        IUnitOfWork uow,
        IDeploymentLogBroadcaster broadcaster,
        INotificationService notification,
        CancellationToken ct)
    {
        deployment.MarkFailed(reason);
        if (project is not null)
            project.RecordDeployment(deployment.Id, DeploymentStatus.Failed);
        await uow.SaveChangesAsync(ct);
        await broadcaster.BroadcastStatusAsync(deployment.Id, "failed", ct);

        if (project is not null)
            await notification.NotifyDeploymentFailed(
                deployment.TenantId, project.Id, deployment.Id, project.Name, reason, ct);
    }

    private static async Task AddLogAsync(
        ApplicationDbContext db,
        IDeploymentLogBroadcaster broadcaster,
        Guid deploymentId,
        string message,
        string? stream = null,
        CancellationToken ct = default)
    {
        try
        {
            var log = new DeploymentLog
            {
                DeploymentId = deploymentId,
                Message = message,
                Level = Domain.Entities.LogLevel.Info,
                Stream = stream,
                Timestamp = DateTime.UtcNow
            };
            db.DeploymentLogs.Add(log);
            await db.SaveChangesAsync(ct);
            await broadcaster.BroadcastLogAsync(deploymentId, message, stream, ct);
        }
        catch { /* Log failures are non-critical */ }
    }

    private static string BuildDeployScript(Project project, Dictionary<string, string> envVars)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("set -e");
        sb.AppendLine($"APP_DIR=\"$HOME/deployflow/{project.Slug}\"");
        sb.AppendLine("mkdir -p \"$APP_DIR\"");
        sb.AppendLine($"cd \"$APP_DIR\"");

        // Environment exports
        foreach (var (key, value) in envVars)
            sb.AppendLine($"export {key}=\"{value.Replace("\"", "\\\"")}\"");

        if (!string.IsNullOrEmpty(project.RepositoryUrl))
        {
            sb.AppendLine($"if [ -d .git ]; then");
            sb.AppendLine($"  git fetch origin");
            sb.AppendLine($"  git reset --hard origin/{project.RepositoryBranch ?? "main"}");
            sb.AppendLine($"else");
            sb.AppendLine($"  git clone --depth=1 --branch {project.RepositoryBranch ?? "main"} {project.RepositoryUrl} .");
            sb.AppendLine($"fi");
        }

        if (!string.IsNullOrEmpty(project.InstallCommand))
            sb.AppendLine(project.InstallCommand);

        if (!string.IsNullOrEmpty(project.BuildCommand))
            sb.AppendLine(project.BuildCommand);

        if (!string.IsNullOrEmpty(project.DockerfilePath))
        {
            var tag = $"deployflow/{project.Slug}:latest";
            sb.AppendLine($"docker build -f {project.DockerfilePath} -t {tag} .");
            sb.AppendLine($"docker stop {project.Slug} 2>/dev/null || true");
            sb.AppendLine($"docker rm {project.Slug} 2>/dev/null || true");

            var portArg = project.Port.HasValue ? $"-p {project.Port}:{project.Port}" : "";
            sb.AppendLine($"docker run -d --name {project.Slug} --restart unless-stopped {portArg} {tag}");
        }
        else if (!string.IsNullOrEmpty(project.StartCommand))
        {
            sb.AppendLine($"pkill -f \"{project.Slug}\" 2>/dev/null || true");
            sb.AppendLine($"nohup {project.StartCommand} > /var/log/{project.Slug}.log 2>&1 &");
        }

        return sb.ToString();
    }
}
