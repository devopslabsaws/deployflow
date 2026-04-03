using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeployFlow.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically checks server health by pinging SSH and updating server status.
/// </summary>
public class ServerHealthCheckService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ServerHealthCheckService> _logger;

    public ServerHealthCheckService(IServiceProvider services, ILogger<ServerHealthCheckService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServerHealthCheckService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllServersAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ServerHealthCheckService");
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task CheckAllServersAsync(CancellationToken ct)
    {
        // Load servers in one scope; then process each server in its own scope to avoid
        // concurrent access to a shared non-thread-safe DbContext.
        List<Guid> serverIds;
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            serverIds = await db.Servers
                .Where(s => !s.IsDeleted && s.SshKeyId != null)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        // Process servers concurrently, each with an isolated scope
        var tasks = serverIds.Select(async serverId =>
        {
            using var scope = _services.CreateScope();
            var db  = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ssh = scope.ServiceProvider.GetRequiredService<ISshService>();
            var enc = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

            var server = await db.Servers.FindAsync(new object[] { serverId }, ct);
            if (server is null) return;

            try
            {
                var key = await db.SshKeys.FirstOrDefaultAsync(k => k.Id == server.SshKeyId!.Value, ct);
                if (key is null) return;

                var privateKey = enc.Decrypt(key.PrivateKeyEncrypted);

                // Compound command: test connectivity AND collect OS / Docker / system metrics in one SSH call.
                // Each line is prefixed with a label so the output is easy to parse robustly.
                var infoCmd =
                    "echo \"OS:$(uname -sr 2>/dev/null)\";" +
                    "echo \"DOCKER:$(docker --version 2>/dev/null | cut -d' ' -f3 | tr -d ',;')\";" +
                    "echo \"LOAD:$(head -1 /proc/loadavg 2>/dev/null | cut -d' ' -f1)\";" +
                    "echo \"MEM:$(free -m 2>/dev/null | grep 'Mem:' | tr -s ' ' | cut -d' ' -f2-3)\";" +
                    "echo \"DISK:$(df -P / 2>/dev/null | tail -1 | tr -s ' ' | cut -d' ' -f5 | tr -d '%')\";" +
                    "echo \"CPU_CORES:$(nproc 2>/dev/null)\";" +
                    "echo \"MEM_TOTAL_MB:$(free -m 2>/dev/null | grep 'Mem:' | tr -s ' ' | cut -d' ' -f2)\";" +
                    "echo \"DISK_TOTAL_GB:$(df -BG / 2>/dev/null | tail -1 | tr -s ' ' | cut -d' ' -f2 | tr -d 'G')\";" +
                    // Azure/AWS/GCP IMDS region detection (1s timeout, silent failure on non-cloud)
                    "echo \"REGION:$(curl -sf --max-time 1 -H 'Metadata:true' 'http://169.254.169.254/metadata/instance/compute/location?api-version=2021-11-01&format=text' 2>/dev/null || echo '')\";";

                var result = await ssh.ExecuteCommandAsync(
                    server.IpAddress, server.SshPort, server.SshUser ?? "root", privateKey, infoCmd, ct);

                if (result.ExitCode == 0 || result.StdOut.Length > 0)
                {
                    // Parse label:value lines into a dictionary
                    var info = result.StdOut
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => l.Contains(':'))
                        .ToDictionary(
                            l => l[..l.IndexOf(':')].ToLowerInvariant(),
                            l => l[(l.IndexOf(':') + 1)..].Trim(),
                            StringComparer.OrdinalIgnoreCase);

                    var os     = info.TryGetValue("os",     out var osV) && !string.IsNullOrWhiteSpace(osV) ? osV : null;
                    var docker = info.TryGetValue("docker", out var dV)  && !string.IsNullOrWhiteSpace(dV)  ? dV  : null;
                    var region = info.TryGetValue("region", out var rV)  && !string.IsNullOrWhiteSpace(rV)  ? rV  : null;

                    double cpuPct = 0;
                    if (info.TryGetValue("load", out var lV) &&
                        double.TryParse(lV, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var loadAvg))
                        cpuPct = Math.Min(loadAvg * 100.0, 100.0);

                    double memPct = 0;
                    if (info.TryGetValue("mem", out var mV))
                    {
                        var mp = mV.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (mp.Length >= 2
                            && double.TryParse(mp[0], out var memTotal)
                            && double.TryParse(mp[1], out var memUsed)
                            && memTotal > 0)
                            memPct = memUsed * 100.0 / memTotal;
                    }

                    double diskPct = 0;
                    if (info.TryGetValue("disk", out var diskV))
                        double.TryParse(diskV, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out diskPct);

                    // Update actual hardware specs from live SSH data
                    int cpuCores = 0, memGb = 0, diskGb = 0;
                    if (info.TryGetValue("cpu_cores", out var coresV))
                        int.TryParse(coresV, out cpuCores);
                    if (info.TryGetValue("mem_total_mb", out var mtV) && int.TryParse(mtV, out var memTotalMb))
                        memGb = (memTotalMb + 512) / 1024; // round MB → GB
                    if (info.TryGetValue("disk_total_gb", out var dtV))
                        int.TryParse(dtV, out diskGb);
                    server.UpdateSpecs(cpuCores, memGb, diskGb);

                    server.SetOnline(docker, os, region);
                    server.UpdateMetrics(cpuPct, memPct, diskPct, server.ActiveContainers);
                }
                else
                {
                    server.SetOffline();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Health check failed for server {Name}", server.Name);
                server.SetOffline();
            }

            await db.SaveChangesAsync(ct);
        });

        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Evaluates alert conditions against current server metrics periodically.
/// </summary>
public class AlertEvaluatorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AlertEvaluatorService> _logger;

    public AlertEvaluatorService(IServiceProvider services, ILogger<AlertEvaluatorService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertEvaluatorService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAlertsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AlertEvaluatorService");
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task EvaluateAlertsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notification = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var servers = await db.Servers.Where(s => !s.IsDeleted).ToListAsync(ct);
        if (servers.Count == 0) return;

        var serverIds = servers.Select(s => s.Id).ToHashSet();

        // Batch-load all active alerts for these servers in ONE query (eliminates N+1)
        var existingAlertKeys = await db.Alerts
            .Where(a => a.Status == AlertStatus.Active && a.ResourceId != null && serverIds.Contains(a.ResourceId.Value))
            .Select(a => new { a.TenantId, a.Source, a.ResourceId })
            .ToListAsync(ct);

        var existingSet = existingAlertKeys
            .Select(a => (a.TenantId, a.Source, ResourceId: a.ResourceId!.Value))
            .ToHashSet();

        var newAlerts = new List<Alert>();
        var notifications = new List<(Guid TenantId, string Name, string Severity)>();

        foreach (var server in servers)
        {
            void Check(bool condition, string subject, AlertSeverity severity, string source)
            {
                if (!condition) return;
                if (existingSet.Contains((server.TenantId, source, server.Id))) return;
                existingSet.Add((server.TenantId, source, server.Id)); // prevent duplicates within same run
                newAlerts.Add(new Alert
                {
                    TenantId   = server.TenantId,
                    Name       = subject,
                    Severity   = severity,
                    Source     = source,
                    Condition  = source == "cpu_usage" ? "> 90%" : source == "memory_usage" ? "> 90%" : "> 85%",
                    ResourceId = server.Id,
                    ResourceType = "Server",
                    Status     = AlertStatus.Active,
                    TriggeredAt = DateTime.UtcNow
                });
                notifications.Add((server.TenantId, subject, severity.ToString()));
            }

            Check(server.CpuUsagePercent > 90,    $"High CPU on {server.Name}",        AlertSeverity.Critical, "cpu_usage");
            Check(server.MemoryUsagePercent > 90, $"High Memory on {server.Name}",      AlertSeverity.Critical, "memory_usage");
            Check(server.DiskUsagePercent > 85,   $"High Disk usage on {server.Name}",  AlertSeverity.Warning,  "disk_usage");
        }

        if (newAlerts.Count > 0)
        {
            await db.Alerts.AddRangeAsync(newAlerts, ct);
            await db.SaveChangesAsync(ct);

            foreach (var (tenantId, name, severity) in notifications)
                await notification.NotifyAlertTriggered(tenantId, name, severity, ct);
        }
    }
}

/// <summary>
/// Collects Docker container metrics from all servers periodically.
/// </summary>
public class ContainerMetricsCollectorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ContainerMetricsCollectorService> _logger;

    public ContainerMetricsCollectorService(IServiceProvider services, ILogger<ContainerMetricsCollectorService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ContainerMetricsCollectorService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectMetricsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ContainerMetricsCollectorService");
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task CollectMetricsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var docker = scope.ServiceProvider.GetRequiredService<IDockerService>();

        // Prune metrics older than 7 days to prevent unbounded table growth
        var cutoff = DateTime.UtcNow.AddDays(-7);
        await db.ServerMetrics
            .Where(m => m.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct);

        var onlineServers = await db.Servers
            .Where(s => !s.IsDeleted && s.Status == ServerStatus.Online)
            .ToListAsync(ct);

        foreach (var server in onlineServers)
        {
            try
            {
                var endpoint = $"tcp://{server.IpAddress}:2376";
                var isUp = await docker.PingServerAsync(endpoint, ct);
                if (!isUp) continue;

                var metrics = new ServerMetrics
                {
                    ServerId = server.Id,
                    Timestamp = DateTime.UtcNow,
                    CpuUsagePercent = server.CpuUsagePercent,
                    MemoryUsageBytes = (long)(server.MemoryUsagePercent / 100.0 * server.MemoryGb * 1_073_741_824L),
                    MemoryTotalBytes = (long)server.MemoryGb * 1_073_741_824L,
                    DiskUsageBytes = (long)(server.DiskUsagePercent / 100.0 * server.DiskGb * 1_073_741_824L),
                    DiskTotalBytes = (long)server.DiskGb * 1_073_741_824L,
                    ActiveContainers = server.ActiveContainers
                };
                await db.ServerMetrics.AddAsync(metrics, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to collect metrics for server {Name}", server.Name);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Monitors server/container health and automatically applies configured recovery rules.
/// Runs every 2 minutes. Supports: restart container, redeploy last-good build, alert-only, scale-up.
/// </summary>
public class ServerAutoRecoveryService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ServerAutoRecoveryService> _logger;

    public ServerAutoRecoveryService(IServiceProvider services, ILogger<ServerAutoRecoveryService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServerAutoRecoveryService started.");
        try
        {
            // stagger slightly to avoid thundering herd with other background services
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRecoveryPassAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ServerAutoRecoveryService");
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task RunRecoveryPassAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var docker = scope.ServiceProvider.GetRequiredService<IDockerService>();
        var logger = _logger;

        // 1. Find servers that have been offline for > 5 minutes
        var staleOfflineThreshold = DateTime.UtcNow.AddMinutes(-5);
        var offlineServers = await db.Servers
            .Where(s => !s.IsDeleted && s.Status == ServerStatus.Offline && s.UpdatedAt < staleOfflineThreshold)
            .ToListAsync(ct);

        // 2. Find recovery rules that are enabled
        var recoveryRules = await db.RecoveryRules
            .Where(r => !r.IsDeleted && r.IsEnabled)
            .ToListAsync(ct);

        foreach (var server in offlineServers)
        {
            var applicableRules = recoveryRules.Where(r =>
                r.TargetServerId == null || r.TargetServerId == server.Id).ToList();

            foreach (var rule in applicableRules.Where(r =>
                r.Trigger == RecoveryTrigger.ServerOffline && r.RetryCount < r.MaxRetries))
            {
                // Enforce per-rule cooldown
                if (rule.LastTriggeredAt.HasValue &&
                    (DateTime.UtcNow - rule.LastTriggeredAt.Value).TotalSeconds < rule.CooldownSeconds)
                    continue;

                logger.LogWarning(
                    "Auto-recovery triggered for server {Server} — rule '{Rule}' action={Action}",
                    server.Name, rule.Name, rule.Action);

                rule.LastTriggeredAt = DateTime.UtcNow;
                rule.RetryCount++;

                switch (rule.Action)
                {
                    case RecoveryAction.RestartContainer:
                        // Restart all containers on the server (best-effort; server may be unreachable)
                        await TryRestartServerContainersAsync(db, docker, server, ct);
                        break;

                    case RecoveryAction.RedeployLastGood:
                        await TryRequeueLastGoodDeploymentAsync(db, server, ct);
                        break;

                    case RecoveryAction.ScaleUp:
                        // Scale-up: mark server for re-provisioning by cloning its spec
                        logger.LogInformation("Scale-up recovery action not yet implemented for server {Id}", server.Id);
                        break;

                    case RecoveryAction.AlertOnly:
                        logger.LogWarning("AlertOnly recovery: server {Name} has been offline since {Since}",
                            server.Name, server.UpdatedAt);
                        break;
                }
            }
        }

        // 3. Handle crashed containers by looking at recent failed deployments
        var recentFailedDeploys = await db.Deployments
            .Where(d => !d.IsDeleted
                && d.Status == DeploymentStatus.Failed
                && d.CreatedAt > DateTime.UtcNow.AddMinutes(-10))
            .ToListAsync(ct);

        foreach (var failedDeploy in recentFailedDeploys)
        {
            var applicableRules = recoveryRules.Where(r =>
                r.Trigger == RecoveryTrigger.DeploymentFailed &&
                (r.TargetProjectId == null || r.TargetProjectId == failedDeploy.ProjectId) &&
                r.RetryCount < r.MaxRetries).ToList();

            foreach (var rule in applicableRules)
            {
                if (rule.LastTriggeredAt.HasValue &&
                    (DateTime.UtcNow - rule.LastTriggeredAt.Value).TotalSeconds < rule.CooldownSeconds)
                    continue;

                rule.LastTriggeredAt = DateTime.UtcNow;
                rule.RetryCount++;

                logger.LogWarning(
                    "DeploymentFailed recovery for deployment {Id} — rule '{Rule}'",
                    failedDeploy.Id, rule.Name);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task TryRestartServerContainersAsync(
        ApplicationDbContext db, IDockerService docker, Server server, CancellationToken ct)
    {
        // Find project IDs that have deployments on this server
        var projectIds = await db.Deployments
            .Where(d => !d.IsDeleted && d.ServerId == server.Id)
            .Select(d => d.ProjectId)
            .Distinct()
            .ToListAsync(ct);

        // List containers for those projects
        var services = await db.Services
            .Where(s => !s.IsDeleted && projectIds.Contains(s.ProjectId) && s.ContainerId != null)
            .ToListAsync(ct);

        foreach (var svc in services)
        {
            try
            {
                await docker.RestartContainerAsync(server.Id.ToString(), svc.ContainerId!, ct);
            }
            catch
            {
                // Best-effort; container or server may be unreachable
            }
        }
    }

    private static async Task TryRequeueLastGoodDeploymentAsync(
        ApplicationDbContext db, Server server, CancellationToken ct)
    {
        // Find the last successful deployment per project on this server and re-queue it
        var lastGoodDeploys = await db.Deployments
            .Where(d => !d.IsDeleted && d.ServerId == server.Id && d.Status == DeploymentStatus.Healthy)
            .GroupBy(d => d.ProjectId)
            .Select(g => g.OrderByDescending(d => d.CreatedAt).First())
            .ToListAsync(ct);

        foreach (var deploy in lastGoodDeploys)
        {
            var requeue = Deployment.Create(
                deploy.TenantId,
                deploy.ProjectId,
                trigger: DeploymentTrigger.Manual,
                branch: deploy.Branch,
                commitSha: deploy.CommitSha,
                commitMessage: "Auto-recovery: re-deploy last healthy build",
                serverId: deploy.ServerId);
            db.Deployments.Add(requeue);
        }
    }
}
