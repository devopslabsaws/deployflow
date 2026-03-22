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
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
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
                    "echo \"DISK:$(df -P / 2>/dev/null | tail -1 | tr -s ' ' | cut -d' ' -f5 | tr -d '%')\"";

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

                    server.SetOnline(docker, os);
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
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
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
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
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
