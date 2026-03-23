using DeployFlow.Application.Common;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/monitoring")]
public class MonitoringController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public MonitoringController(
        IMediator mediator,
        ApplicationDbContext db,
        ICurrentUser currentUser)
        : base(mediator)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid? serverId = null,
        [FromQuery] string range = "1h",
        CancellationToken ct = default)
    {
        var from = ResolveFrom(range);

        var serverIdsQuery = _db.Servers
            .AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .Select(s => s.Id);

        if (serverId.HasValue)
        {
            var belongs = await _db.Servers.AnyAsync(
                s => s.Id == serverId.Value && s.TenantId == _currentUser.TenantId,
                ct);

            if (!belongs)
                return NotFound(new { error = "Server not found." });

            serverIdsQuery = _db.Servers.AsNoTracking().Where(s => s.Id == serverId.Value).Select(s => s.Id);
        }

        var metrics = await _db.ServerMetrics
            .AsNoTracking()
            .Where(m => serverIdsQuery.Contains(m.ServerId) && m.Timestamp >= from)
            .OrderByDescending(m => m.Timestamp)
            .Take(2000)
            .ToListAsync(ct);

        if (metrics.Count == 0)
        {
            return Ok(new
            {
                avgCpu = 0.0,
                avgMemory = 0.0,
                avgDisk = 0.0,
                avgNetworkInMbps = 0.0,
                avgNetworkOutMbps = 0.0,
                sampleCount = 0
            });
        }

        var avgCpu = metrics.Average(m => m.CpuUsagePercent);
        var avgMemory = metrics.Average(m => m.MemoryTotalBytes > 0
            ? (m.MemoryUsageBytes * 100.0 / m.MemoryTotalBytes)
            : 0);
        var avgDisk = metrics.Average(m => m.DiskTotalBytes > 0
            ? (m.DiskUsageBytes * 100.0 / m.DiskTotalBytes)
            : 0);

        var netIn = metrics.Average(m => m.NetworkRxBytes) / 1024d / 1024d;
        var netOut = metrics.Average(m => m.NetworkTxBytes) / 1024d / 1024d;

        return Ok(new
        {
            avgCpu = Math.Round(avgCpu, 2),
            avgMemory = Math.Round(avgMemory, 2),
            avgDisk = Math.Round(avgDisk, 2),
            avgNetworkInMbps = Math.Round(netIn, 2),
            avgNetworkOutMbps = Math.Round(netOut, 2),
            sampleCount = metrics.Count
        });
    }

    [HttpGet("timeseries")]
    public async Task<IActionResult> GetTimeSeries(
        [FromQuery] string metric = "cpu",
        [FromQuery] Guid? serverId = null,
        [FromQuery] string range = "1h",
        CancellationToken ct = default)
    {
        var from = ResolveFrom(range);

        var query = BaseMetricsQuery(serverId, from);
        var raw = await query
            .OrderBy(m => m.Timestamp)
            .Take(5000)
            .ToListAsync(ct);

        var grouped = raw
            .GroupBy(m => new DateTime(m.Timestamp.Year, m.Timestamp.Month, m.Timestamp.Day, m.Timestamp.Hour, m.Timestamp.Minute, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                timestamp = g.Key,
                value = metric.ToLowerInvariant() switch
                {
                    "memory" => g.Average(x => x.MemoryTotalBytes > 0 ? (x.MemoryUsageBytes * 100.0 / x.MemoryTotalBytes) : 0),
                    "disk" => g.Average(x => x.DiskTotalBytes > 0 ? (x.DiskUsageBytes * 100.0 / x.DiskTotalBytes) : 0),
                    "net-in" => g.Average(x => x.NetworkRxBytes) / 1024d / 1024d,
                    "net-out" => g.Average(x => x.NetworkTxBytes) / 1024d / 1024d,
                    _ => g.Average(x => x.CpuUsagePercent),
                }
            })
            .ToList();

        return Ok(grouped.Select(x => new
        {
            x.timestamp,
            value = Math.Round(x.value, 2)
        }));
    }

    [HttpGet("network")]
    public async Task<IActionResult> GetNetwork(
        [FromQuery] Guid? serverId = null,
        [FromQuery] string range = "1h",
        CancellationToken ct = default)
    {
        var from = ResolveFrom(range);

        var query = BaseMetricsQuery(serverId, from);
        var raw = await query
            .OrderBy(m => m.Timestamp)
            .Take(5000)
            .ToListAsync(ct);

        var grouped = raw
            .GroupBy(m => new DateTime(m.Timestamp.Year, m.Timestamp.Month, m.Timestamp.Day, m.Timestamp.Hour, m.Timestamp.Minute, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                timestamp = g.Key,
                inbound = g.Average(x => x.NetworkRxBytes) / 1024d / 1024d,
                outbound = g.Average(x => x.NetworkTxBytes) / 1024d / 1024d,
            })
            .ToList();

        return Ok(grouped.Select(x => new
        {
            x.timestamp,
            inbound = Math.Round(x.inbound, 2),
            outbound = Math.Round(x.outbound, 2)
        }));
    }

    private IQueryable<Domain.Entities.ServerMetrics> BaseMetricsQuery(Guid? serverId, DateTime from)
    {
        var serverIdsQuery = _db.Servers
            .AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .Select(s => s.Id);

        if (serverId.HasValue)
            serverIdsQuery = _db.Servers.AsNoTracking().Where(s => s.Id == serverId.Value && s.TenantId == _currentUser.TenantId).Select(s => s.Id);

        return _db.ServerMetrics
            .AsNoTracking()
            .Where(m => serverIdsQuery.Contains(m.ServerId) && m.Timestamp >= from);
    }

    private static DateTime ResolveFrom(string range)
    {
        return range.ToLowerInvariant() switch
        {
            "15m" => DateTime.UtcNow.AddMinutes(-15),
            "6h" => DateTime.UtcNow.AddHours(-6),
            "24h" => DateTime.UtcNow.AddHours(-24),
            _ => DateTime.UtcNow.AddHours(-1),
        };
    }
}
