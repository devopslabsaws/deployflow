using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Application.Features.Deployments.Commands;

namespace DeployFlow.Infrastructure.Services;

/// <summary>
/// Runs a battery of pre-deployment readiness checks against a <see cref="Server"/>:
/// SSH reachability, Docker availability, disk space, memory, and port availability.
/// Each check is fire-and-forget with a timeout so the entire report completes in &lt; 15 s.
/// </summary>
public class PreflightCheckService : IPreflightCheckService
{
    private readonly ISshService _ssh;

    public PreflightCheckService(ISshService ssh) => _ssh = ssh;

    public async Task<PreflightReport> RunAsync(Server server, string privateKey, int? port, CancellationToken ct)
    {
        var checks = new List<PreflightCheckResult>();

        // 1. SSH reachability
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));
            var ok = await _ssh.TestConnectionAsync(server.IpAddress, server.SshPort, server.SshUser, privateKey, cts.Token);
            checks.Add(ok
                ? new("SSH Connection", "pass", $"Reached {server.IpAddress}:{server.SshPort}")
                : new("SSH Connection", "fail", $"Could not connect to {server.IpAddress}:{server.SshPort}"));
        }
        catch
        {
            checks.Add(new("SSH Connection", "fail", "SSH connection timed out"));
            // If SSH fails, remaining remote checks would also fail — skip them
            return BuildReport(checks);
        }

        // 2–5. Remote checks run in parallel via separate SSH commands
        var tasks = new[]
        {
            RunRemoteCheckAsync(server, privateKey, "Docker Status",
                "docker info --format 'version={{.ServerVersion}}' 2>&1",
                @out: stdout =>
                {
                    if (stdout.Contains("Cannot connect") || stdout.Contains("permission denied"))
                        return new("Docker Status", "fail", "Docker daemon is not running or not accessible");
                    if (stdout.Contains("version="))
                        return new("Docker Status", "pass", stdout.Trim());
                    return new("Docker Status", "warn", "Docker may not be installed");
                }, ct),

            RunRemoteCheckAsync(server, privateKey, "Disk Space",
                "df -h / | awk 'NR==2{print $4,$5}'",
                @out: stdout =>
                {
                    // e.g. "18G 42%"
                    var parts = stdout.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) return new("Disk Space", "warn", "Could not parse disk usage");
                    var available = parts[0];
                    var used      = parts[1].TrimEnd('%');
                    if (int.TryParse(used, out var pct))
                    {
                        if (pct >= 92) return new("Disk Space", "fail", $"Disk nearly full: {pct}% used, {available} free");
                        if (pct >= 80) return new("Disk Space", "warn", $"{pct}% used, {available} free — consider freeing space");
                        return new("Disk Space", "pass", $"{available} free ({pct}% used)");
                    }
                    return new("Disk Space", "warn", stdout.Trim());
                }, ct),

            RunRemoteCheckAsync(server, privateKey, "Memory",
                "free -m | awk 'NR==2{printf \"%d %d\", $3, $2}'",
                @out: stdout =>
                {
                    var parts = stdout.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) return new("Memory", "warn", "Could not read memory stats");
                    if (int.TryParse(parts[0], out var used) && int.TryParse(parts[1], out var total) && total > 0)
                    {
                        var free = total - used;
                        var pct  = (used * 100) / total;
                        if (free < 256)  return new("Memory", "fail", $"Only {free} MB free — deploy may fail");
                        if (free < 512)  return new("Memory", "warn", $"{free} MB free ({pct}% used)");
                        return new("Memory", "pass", $"{free} MB free ({pct}% used)");
                    }
                    return new("Memory", "warn", stdout.Trim());
                }, ct),

            port.HasValue
                ? RunRemoteCheckAsync(server, privateKey, "Port Availability",
                    $"ss -tlnp 'sport = :{port}' 2>/dev/null | tail -n +2",
                    @out: stdout =>
                    {
                        if (string.IsNullOrWhiteSpace(stdout))
                            return new("Port Availability", "pass", $"Port {port} is free");
                        return new("Port Availability", "warn", $"Port {port} is already in use — container may conflict");
                    }, ct)
                : Task.FromResult(new PreflightCheckResult("Port Availability", "pass", "No port configured — skipped")),
        };

        var results = await Task.WhenAll(tasks);
        checks.AddRange(results);

        return BuildReport(checks);
    }

    private async Task<PreflightCheckResult> RunRemoteCheckAsync(
        Server server, string privateKey, string name, string command,
        Func<string, PreflightCheckResult> @out, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            var result = await _ssh.ExecuteCommandAsync(
                server.IpAddress, server.SshPort, server.SshUser, privateKey, command, cts.Token);
            return @out(result.StdOut + result.StdErr);
        }
        catch
        {
            return new(name, "warn", "Check timed out or SSH error");
        }
    }

    private static PreflightReport BuildReport(IReadOnlyList<PreflightCheckResult> checks)
    {
        var canDeploy = checks.All(c => !c.Failed);
        return new PreflightReport(canDeploy, checks, DateTime.UtcNow.ToString("o"));
    }
}
