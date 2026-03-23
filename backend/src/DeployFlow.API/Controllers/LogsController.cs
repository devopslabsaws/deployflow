using System.Text.Json;
using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/logs")]
public class LogsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public LogsController(
        IMediator mediator,
        ApplicationDbContext db,
        ICurrentUser currentUser)
        : base(mediator)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? level = null,
        [FromQuery] string? search = null,
        [FromQuery] string? service = null,
        [FromQuery] long? cursor = null,
        [FromQuery] int pageSize = 200,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 20, 1000);

        var deploymentIds = _db.Deployments
            .AsNoTracking()
            .Where(d => d.TenantId == _currentUser.TenantId)
            .Select(d => d.Id);

        var query = _db.DeploymentLogs
            .AsNoTracking()
            .Where(l => deploymentIds.Contains(l.DeploymentId));

        if (!string.IsNullOrWhiteSpace(level) && Enum.TryParse<DeployFlow.Domain.Entities.LogLevel>(level, true, out var parsedLevel))
            query = query.Where(l => l.Level == parsedLevel);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l => l.Message.Contains(search));

        if (!string.IsNullOrWhiteSpace(service))
            query = query.Where(l => l.Stream == service);

        if (cursor.HasValue)
        {
            var cursorTime = DateTimeOffset.FromUnixTimeMilliseconds(cursor.Value).UtcDateTime;
            query = query.Where(l => l.Timestamp < cursorTime);
        }

        var rows = await query
            .OrderByDescending(l => l.Timestamp)
            .Take(pageSize)
            .Select(l => new LogLineDto(
                l.Id,
                l.Timestamp,
                l.Level.ToString().ToLowerInvariant(),
                l.Message,
                l.Stream ?? "deployment"))
            .ToListAsync(ct);

        var nextCursor = rows.Count > 0
            ? new DateTimeOffset(rows[^1].Timestamp).ToUnixTimeMilliseconds()
            : (long?)null;

        return Ok(new
        {
            items = rows,
            nextCursor,
            hasMore = rows.Count == pageSize
        });
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export(
        [FromBody] LogExportRequest req,
        CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(req.Limit ?? 5000, 1, 50000);

        var deploymentIds = _db.Deployments
            .AsNoTracking()
            .Where(d => d.TenantId == _currentUser.TenantId)
            .Select(d => d.Id);

        var query = _db.DeploymentLogs
            .AsNoTracking()
            .Where(l => deploymentIds.Contains(l.DeploymentId));

        if (!string.IsNullOrWhiteSpace(req.Level) && Enum.TryParse<DeployFlow.Domain.Entities.LogLevel>(req.Level, true, out var parsedLevel))
            query = query.Where(l => l.Level == parsedLevel);

        if (!string.IsNullOrWhiteSpace(req.Search))
            query = query.Where(l => l.Message.Contains(req.Search));

        if (!string.IsNullOrWhiteSpace(req.Service))
            query = query.Where(l => l.Stream == req.Service);

        if (req.From.HasValue)
            query = query.Where(l => l.Timestamp >= req.From.Value.UtcDateTime);

        if (req.To.HasValue)
            query = query.Where(l => l.Timestamp <= req.To.Value.UtcDateTime);

        var rows = await query
            .OrderByDescending(l => l.Timestamp)
            .Take(pageSize)
            .Select(l => new { l.Timestamp, Level = l.Level.ToString(), l.Message, Service = l.Stream ?? "deployment" })
            .ToListAsync(ct);

        var lines = rows.Select(r => $"[{r.Timestamp:O}] [{r.Level.ToUpperInvariant()}] [{r.Service}] {r.Message}");
        var content = string.Join("\n", lines);
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var fileName = $"logs-{DateTime.UtcNow:yyyy-MM-dd}.txt";

        return File(bytes, "text/plain; charset=utf-8", fileName);
    }

    [HttpGet("stream")]
    public async Task Stream(
        [FromQuery] string? level = null,
        [FromQuery] string? service = null,
        CancellationToken ct = default)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var writer = new StreamWriter(Response.Body, leaveOpen: true) { AutoFlush = false };

        DateTime lastTimestamp = DateTime.UtcNow.AddSeconds(-10);

        while (!ct.IsCancellationRequested)
        {
            var deploymentIds = _db.Deployments
                .AsNoTracking()
                .Where(d => d.TenantId == _currentUser.TenantId)
                .Select(d => d.Id);

            var query = _db.DeploymentLogs
                .AsNoTracking()
                .Where(l => deploymentIds.Contains(l.DeploymentId) && l.Timestamp > lastTimestamp);

            if (!string.IsNullOrWhiteSpace(level) && Enum.TryParse<DeployFlow.Domain.Entities.LogLevel>(level, true, out var parsedLevel))
                query = query.Where(l => l.Level == parsedLevel);

            if (!string.IsNullOrWhiteSpace(service))
                query = query.Where(l => l.Stream == service);

            var logs = await query
                .OrderBy(l => l.Timestamp)
                .Take(200)
                .ToListAsync(ct);

            foreach (var log in logs)
            {
                lastTimestamp = log.Timestamp;
                var payload = JsonSerializer.Serialize(new LogLineDto(
                    log.Id,
                    log.Timestamp,
                    log.Level.ToString().ToLowerInvariant(),
                    log.Message,
                    log.Stream ?? "deployment"));

                await writer.WriteAsync($"event: log\n");
                await writer.WriteAsync($"data: {payload}\n\n");
            }

            await writer.WriteAsync("event: heartbeat\ndata: {}\n\n");
            await writer.FlushAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}

public record LogLineDto(
    Guid Id,
    DateTime Timestamp,
    string Level,
    string Message,
    string Service);

public record LogExportRequest(
    string? Level,
    string? Search,
    string? Service,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Limit);
