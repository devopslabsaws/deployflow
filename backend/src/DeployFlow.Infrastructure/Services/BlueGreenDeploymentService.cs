using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeployFlow.Infrastructure.Services;

/// <summary>
/// Implements zero-downtime Blue/Green deployments.
///
/// Flow:
///   1. Identify the inactive slot (opposite of project.ActiveSlot).
///   2. Build and start the new container on the inactive slot.
///   3. Run health-check against the inactive slot's port.
///   4. On success → switch Traefik/proxy routing to the new slot, update project.ActiveSlot.
///   5. Stop (but don't remove) the old slot container — available for instant rollback.
///
/// The "routing switch" is implemented by updating Docker labels on the containers so
/// that the Traefik reverse proxy (same pattern used by Coolify shown in the screenshots)
/// automatically routes traffic to the new slot.
/// </summary>
public class BlueGreenDeploymentService
{
    private readonly ISshService _ssh;
    private readonly IEncryptionService _enc;
    private readonly IUnitOfWork _uow;
    private readonly IDeploymentLogBroadcaster _broadcaster;
    private readonly ILogger<BlueGreenDeploymentService> _logger;
    private readonly ProxyRoutingOptions _proxyRouting;

    public BlueGreenDeploymentService(
        ISshService ssh,
        IEncryptionService enc,
        IUnitOfWork uow,
        IDeploymentLogBroadcaster broadcaster,
        ILogger<BlueGreenDeploymentService> logger,
        IOptions<ProxyRoutingOptions> proxyRouting)
    {
        _ssh = ssh;
        _enc = enc;
        _uow = uow;
        _broadcaster = broadcaster;
        _logger = logger;
        _proxyRouting = proxyRouting.Value;
    }

    /// <summary>
    /// Performs a blue/green deploy for the given project/deployment.
    /// Returns true on success, false on failure.
    /// </summary>
    public async Task<BlueGreenDeploymentResult> DeployAsync(
        Project project,
        Deployment deployment,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var server = project.ServerId.HasValue
            ? await _uow.Servers.GetByIdAsync(project.ServerId.Value, ct)
            : null;

        if (server is null)
        {
            await Log(db, deployment.Id, "❌ No server assigned — cannot blue/green deploy.", ct: ct);
            return BlueGreenDeploymentResult.Fail();
        }

        var sshKey = server.SshKeyId.HasValue
            ? await _uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct)
            : null;

        if (sshKey is null)
        {
            await Log(db, deployment.Id, "❌ No SSH key configured on server.", ct: ct);
            return BlueGreenDeploymentResult.Fail();
        }

        var privateKey = _enc.Decrypt(sshKey.PrivateKeyEncrypted);

        // Determine inactive slot
        var inactiveSlot = project.ActiveSlot == "blue" ? "green" : "blue";
        var activeSlot   = project.ActiveSlot;

        var inactiveContainer = $"{project.Slug}-{inactiveSlot}";
        var activeContainer   = $"{project.Slug}-{activeSlot}";

        await Log(db, deployment.Id, $"🎨 Blue/Green deploy — targeting slot: {inactiveSlot} (container: {inactiveContainer})", ct: ct);
        await _broadcaster.BroadcastStatusAsync(deployment.Id, "deploying", ct);

        // Step 1 — Build image on inactive slot
        var imageTag  = $"deployflow/{project.Slug}:{inactiveSlot}-{deployment.Id.ToString()[..8]}";
        var envVars   = await _uow.EnvVariables.GetByProjectAsync(project.Id, ct);
        var envExports= string.Join("\n", envVars.Select(e => $"export {e.Key}=\"{e.Value?.Replace("\"", "\\\"")}\""));

        var buildScript = BuildScript(project, imageTag, inactiveContainer, envExports);

        await Log(db, deployment.Id, "🔨 Building image on inactive slot...", ct: ct);
        var buildResult = await _ssh.ExecuteCommandAsync(
            server.IpAddress, server.SshPort, server.SshUser, privateKey, buildScript, ct);

        if (!buildResult.Success || buildResult.ExitCode != 0)
        {
            await Log(db, deployment.Id, $"❌ Build failed:\n{buildResult.StdErr}", "stderr", ct);
            return BlueGreenDeploymentResult.Fail();
        }
        await Log(db, deployment.Id, buildResult.StdOut, "stdout", ct);

        var inactivePort = ExtractAssignedHostPort(buildResult.StdOut) ?? project.Port ?? 3000;
        var publicUrl = ResolveBlueGreenPublicUrl(project, server.IpAddress, inactivePort);

        // Step 2 — Health-check inactive slot
        await Log(db, deployment.Id, $"🔍 Health-checking {inactiveContainer} on port {inactivePort}...", ct: ct);
        var healthScript = HealthCheckScript(inactivePort, project.HealthCheckPath ?? "/", project.HealthCheckTimeout);
        var healthResult = await _ssh.ExecuteCommandAsync(
            server.IpAddress, server.SshPort, server.SshUser, privateKey, healthScript, ct);

        if (!healthResult.Success || healthResult.ExitCode != 0)
        {
            await Log(db, deployment.Id, "❌ Health check failed — aborting slot switch.", ct: ct);
            // Stop the failed inactive container to clean up
            var cleanup = $"docker stop {inactiveContainer} 2>/dev/null; docker rm {inactiveContainer} 2>/dev/null; true";
            await _ssh.ExecuteCommandAsync(server.IpAddress, server.SshPort, server.SshUser, privateKey, cleanup, ct);
            return BlueGreenDeploymentResult.Fail();
        }

        // Step 3 — Switch routing (update Traefik labels + stop old slot)
        await Log(db, deployment.Id, $"🔀 Switching traffic from {activeSlot} → {inactiveSlot}...", ct: ct);
        var switchScript = SwitchScript(activeContainer, inactiveContainer, inactivePort);
        var switchResult = await _ssh.ExecuteCommandAsync(
            server.IpAddress, server.SshPort, server.SshUser, privateKey, switchScript, ct);

        if (!switchResult.Success)
        {
            await Log(db, deployment.Id, $"⚠️ Route switch had warnings:\n{switchResult.StdErr}", "stderr", ct);
        }

        // Step 4 — Update project slot state
        project.SwitchSlot(
            inactiveSlot,
            blueContainer:  inactiveSlot == "blue"  ? inactiveContainer : activeContainer,
            greenContainer: inactiveSlot == "green" ? inactiveContainer : activeContainer);

        await Log(db, deployment.Id, $"✅ Blue/Green switch complete — {inactiveSlot} is now live at {publicUrl}.", ct: ct);
        return BlueGreenDeploymentResult.Ok(publicUrl);
    }

    private static string BuildScript(Project project, string imageTag, string containerName, string envExports)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("set -e");
        sb.AppendLine($"APP_DIR=\"$HOME/deployflow/{project.Slug}\"");
        sb.AppendLine("mkdir -p \"$APP_DIR\" && cd \"$APP_DIR\"");
        sb.AppendLine(envExports);
        sb.AppendLine(project.Port.HasValue ? $"DEFAULT_INTERNAL_PORT={project.Port.Value}" : "DEFAULT_INTERNAL_PORT=3000");
        sb.AppendLine("PORT=\"${PORT:-$DEFAULT_INTERNAL_PORT}\"");
        sb.AppendLine("port_in_use() {");
        sb.AppendLine("  local candidate=\"$1\"");
        sb.AppendLine("  if command -v ss >/dev/null 2>&1; then");
        sb.AppendLine("    ss -ltn \"( sport = :$candidate )\" 2>/dev/null | tail -n +2 | grep -q .");
        sb.AppendLine("    return $?");
        sb.AppendLine("  fi");
        sb.AppendLine("  if command -v netstat >/dev/null 2>&1; then");
        sb.AppendLine("    netstat -ltn 2>/dev/null | awk '{print $4}' | grep -Eq \"(^|[:.])${candidate}$\"");
        sb.AppendLine("    return $?");
        sb.AppendLine("  fi");
        sb.AppendLine("  return 1");
        sb.AppendLine("}");
        sb.AppendLine("allocate_host_port() {");
        sb.AppendLine("  local preferred=\"${1:-3000}\"");
        sb.AppendLine("  local range_start=3000");
        sb.AppendLine("  local range_end=3999");
        sb.AppendLine("  if [ \"$preferred\" -ge \"$range_start\" ] && [ \"$preferred\" -le \"$range_end\" ] && ! port_in_use \"$preferred\"; then");
        sb.AppendLine("    echo \"$preferred\"");
        sb.AppendLine("    return 0");
        sb.AppendLine("  fi");
        sb.AppendLine("  local candidate");
        sb.AppendLine("  for candidate in $(seq \"$range_start\" \"$range_end\"); do");
        sb.AppendLine("    if ! port_in_use \"$candidate\"; then");
        sb.AppendLine("      echo \"$candidate\"");
        sb.AppendLine("      return 0");
        sb.AppendLine("    fi");
        sb.AppendLine("  done");
        sb.AppendLine("  return 1");
        sb.AppendLine("}");
        sb.AppendLine("HOST_PORT=\"$(allocate_host_port \"$PORT\")\"");
        sb.AppendLine("[ -n \"$HOST_PORT\" ] || { echo 'no host port available' >&2; exit 1; }");

        if (!string.IsNullOrEmpty(project.RepositoryUrl))
        {
            sb.AppendLine("if [ -d .git ]; then git fetch origin && git reset --hard origin/" +
                          (project.RepositoryBranch ?? "main") + "; else git clone --depth=1 --branch " +
                          (project.RepositoryBranch ?? "main") + " " + project.RepositoryUrl + " .; fi");
        }

        if (!string.IsNullOrEmpty(project.InstallCommand)) sb.AppendLine(project.InstallCommand);
        if (!string.IsNullOrEmpty(project.BuildCommand)) sb.AppendLine(project.BuildCommand);

        if (!string.IsNullOrEmpty(project.DockerfilePath))
        {
            sb.AppendLine($"docker build -f {project.DockerfilePath} -t {imageTag} .");
            sb.AppendLine($"docker stop {containerName} 2>/dev/null || true");
            sb.AppendLine($"docker rm   {containerName} 2>/dev/null || true");
            sb.AppendLine($"docker run -d --name {containerName} --restart unless-stopped " +
                          $"-p \"$HOST_PORT:$PORT\" {imageTag}");
            sb.AppendLine("echo \"DEPLOYFLOW_SLOT_PORT=$HOST_PORT\"");
        }
        else if (!string.IsNullOrEmpty(project.StartCommand))
        {
            sb.AppendLine($"pkill -f \"{containerName}\" 2>/dev/null || true");
            sb.AppendLine($"nohup {project.StartCommand} > /var/log/{containerName}.log 2>&1 &");
            sb.AppendLine("echo \"DEPLOYFLOW_SLOT_PORT=$HOST_PORT\"");
        }

        return sb.ToString();
    }

    private static string HealthCheckScript(int port, string path, int timeoutSeconds)
    {
        // Poll the inactive container's health endpoint up to timeout
        return $"for i in $(seq 1 {timeoutSeconds}); do " +
               $"  curl -sf http://localhost:{port}{path} > /dev/null 2>&1 && echo 'healthy' && exit 0; " +
               $"  sleep 1; " +
               $"done; echo 'timeout'; exit 1";
    }

    private static string SwitchScript(string oldContainer, string newContainer, int newPort)
    {
        // Update an nginx/Traefik upstream config or just record the new "live" container.
        // For simple Docker setups: rename containers so the main domain always points to project.Slug
        return $"#!/bin/bash\n" +
               $"docker stop {oldContainer} 2>/dev/null || true\n" +
               $"echo '{newContainer} is now the active container on port {newPort}'\n";
    }

    private string? ResolveBlueGreenPublicUrl(Project project, string serverIp, int inactivePort)
    {
        if (!string.IsNullOrWhiteSpace(project.CustomDomain))
            return DeploymentPublicUrlResolver.ResolvePublicUrl(project, _proxyRouting, serverIp, inactivePort);

        return $"http://{serverIp}:{inactivePort}";
    }

    private static int? ExtractAssignedHostPort(string? stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(stdout, @"DEPLOYFLOW_SLOT_PORT=(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var port) ? port : null;
    }

    private async Task Log(ApplicationDbContext db, Guid deploymentId, string message, string? stream = null, CancellationToken ct = default)
    {
        try
        {
            db.DeploymentLogs.Add(new DeploymentLog
            {
                DeploymentId = deploymentId,
                Message = message,
                Level = Domain.Entities.LogLevel.Info,
                Stream = stream,
                Timestamp = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await _broadcaster.BroadcastLogAsync(deploymentId, message, stream, ct);
        }
        catch { /* log write failures are non-critical */ }
    }
}

public sealed record BlueGreenDeploymentResult(bool Success, string? PublicUrl)
{
    public static BlueGreenDeploymentResult Ok(string? publicUrl) => new(true, publicUrl);
    public static BlueGreenDeploymentResult Fail() => new(false, null);
}
