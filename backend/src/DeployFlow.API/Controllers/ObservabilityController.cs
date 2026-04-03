using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;
using System.Text.RegularExpressions;

namespace DeployFlow.API.Controllers;

/// <summary>OTel distributed tracing ingestion + log aggregation rules + crash reports.</summary>
[Authorize]
[Route("api/observability")]
public class ObservabilityController : BaseController
{
    private readonly ApplicationDbContext _db;

    public ObservabilityController(IMediator mediator, ApplicationDbContext db) : base(mediator) => _db = db;

    // ── Traces ────────────────────────────────────────────────────────────────

    [HttpGet("traces")]
    public async Task<IActionResult> ListTraces(
        [FromQuery] Guid? projectId,
        [FromQuery] string? serviceName,
        [FromQuery] TraceStatus? status,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var q = _db.OtelTraces.AsQueryable();
        if (projectId.HasValue) q = q.Where(t => t.ProjectId == projectId.Value);
        if (!string.IsNullOrWhiteSpace(serviceName)) q = q.Where(t => t.ServiceName == serviceName);
        if (status.HasValue) q = q.Where(t => t.Status == status.Value);
        return Ok(await q.OrderByDescending(t => t.CreatedAt).Take(limit).ToListAsync(ct));
    }

    [HttpGet("traces/{id:guid}")]
    public async Task<IActionResult> GetTrace(Guid id, CancellationToken ct)
    {
        var trace = await _db.OtelTraces.FirstOrDefaultAsync(t => t.Id == id, ct);
        return trace is null ? NotFound() : Ok(trace);
    }

    /// <summary>Ingest a batch of OTel spans from an instrumented service.</summary>
    [HttpPost("traces/ingest")]
    [AllowAnonymous] // collector endpoint — auth via X-API-Key header in production
    public async Task<IActionResult> IngestTrace([FromBody] IngestTraceRequest req, CancellationToken ct)
    {
        var trace = new OtelTrace
        {
            TenantId = req.TenantId,
            TraceId = req.TraceId,
            RootSpanId = req.RootSpanId,
            ServiceName = req.ServiceName,
            Environment = req.Environment,
            ProjectId = req.ProjectId,
            OperationName = req.OperationName,
            Status = req.ErrorSpanCount > 0 ? TraceStatus.Error : TraceStatus.Ok,
            DurationMs = req.DurationMs,
            SpanCount = req.SpanCount,
            ErrorSpanCount = req.ErrorSpanCount,
            HttpMethod = req.HttpMethod,
            HttpUrl = req.HttpUrl,
            HttpStatusCode = req.HttpStatusCode,
            ErrorMessage = req.ErrorMessage,
            SpansJson = req.SpansJson,
        };
        _db.OtelTraces.Add(trace);
        await _db.SaveChangesAsync(ct);
        return Ok(new { traceId = trace.TraceId, id = trace.Id });
    }

    [HttpGet("traces/summary")]
    public async Task<IActionResult> TraceSummary([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var q = _db.OtelTraces.AsQueryable();
        if (projectId.HasValue) q = q.Where(t => t.ProjectId == projectId.Value);
        var all = await q.ToListAsync(ct);
        return Ok(new
        {
            total = all.Count,
            errors = all.Count(t => t.Status == TraceStatus.Error),
            avgDurationMs = all.Count > 0 ? (long)all.Average(t => t.DurationMs) : 0,
            p99DurationMs = all.Count > 0 ? all.OrderByDescending(t => t.DurationMs).Skip((int)(all.Count * 0.01)).FirstOrDefault()?.DurationMs ?? 0 : 0,
            services = all.Select(t => t.ServiceName).Distinct().ToList(),
        });
    }

    // ── Log Aggregation Rules ────────────────────────────────────────────────

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var q = _db.LogAggregationRules.Where(r => !r.IsDeleted);
        if (projectId.HasValue) q = q.Where(r => r.ProjectId == projectId.Value);
        return Ok(await q.OrderBy(r => r.Name).ToListAsync(ct));
    }

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest req, CancellationToken ct)
    {
        // Validate regex
        try { _ = new Regex(req.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)); }
        catch { return BadRequest(new { error = "Invalid regex pattern." }); }

        var rule = new LogAggregationRule
        {
            TenantId = req.TenantId,
            ProjectId = req.ProjectId,
            Name = req.Name,
            Pattern = req.Pattern,
            Severity = req.Severity,
            TriggerAlert = req.TriggerAlert,
            SummarizeCrash = req.SummarizeCrash,
        };
        _db.LogAggregationRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return Ok(rule);
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] CreateRuleRequest req, CancellationToken ct)
    {
        var rule = await _db.LogAggregationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NotFound();
        rule.Name = req.Name;
        rule.Pattern = req.Pattern;
        rule.Severity = req.Severity;
        rule.TriggerAlert = req.TriggerAlert;
        rule.SummarizeCrash = req.SummarizeCrash;
        rule.Touch();
        await _db.SaveChangesAsync(ct);
        return Ok(rule);
    }

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        var rule = await _db.LogAggregationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NotFound();
        rule.SoftDelete();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Crash Reports ─────────────────────────────────────────────────────────

    [HttpGet("crashes")]
    public async Task<IActionResult> GetCrashes([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var q = _db.CrashReports.Where(c => !c.IsDeleted);
        if (projectId.HasValue) q = q.Where(c => c.ProjectId == projectId.Value);
        return Ok(await q.OrderByDescending(c => c.LastSeenAt ?? c.FirstSeenAt).ToListAsync(ct));
    }

    /// <summary>Ingest a crash report from the log streaming buffer.</summary>
    [HttpPost("crashes")]
    [AllowAnonymous]
    public async Task<IActionResult> IngestCrash([FromBody] IngestCrashRequest req, CancellationToken ct)
    {
        // Deduplicate by pattern + project — increment occurrence count
        var existing = await _db.CrashReports.FirstOrDefaultAsync(
            c => c.ProjectId == req.ProjectId && c.Pattern == req.Pattern && !c.IsResolved, ct);

        if (existing is not null)
        {
            existing.OccurrenceCount++;
            existing.LastSeenAt = DateTime.UtcNow;
            existing.RawLogSample = req.RawLogSample ?? existing.RawLogSample;
            existing.Touch();
            await _db.SaveChangesAsync(ct);
            return Ok(existing);
        }

        var crash = new CrashReport
        {
            TenantId = req.TenantId,
            ProjectId = req.ProjectId,
            ServiceName = req.ServiceName,
            Pattern = req.Pattern,
            RawLogSample = req.RawLogSample,
            RcaSummary = BuildRcaSummary(req.Pattern, req.RawLogSample),
            LastSeenAt = DateTime.UtcNow,
        };
        _db.CrashReports.Add(crash);
        await _db.SaveChangesAsync(ct);
        return Ok(crash);
    }

    [HttpPost("crashes/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveCrash(Guid id, [FromBody] ResolveCrashRequest req, CancellationToken ct)
    {
        var crash = await _db.CrashReports.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (crash is null) return NotFound();
        crash.IsResolved = true;
        crash.Resolution = req.Resolution;
        crash.Touch();
        await _db.SaveChangesAsync(ct);
        return Ok(crash);
    }

    // ── Rule-based RCA ───────────────────────────────────────────────────────

    private static string BuildRcaSummary(string pattern, string? sample)
    {
        if (sample is null) return $"Pattern matched: {pattern}";
        // Rule-based pattern→cause mapping
        var rules = new Dictionary<string, string>
        {
            ["out of memory|OOM|killed"]               = "Process was OOM-killed. Increase memory limits or add a ScalingPolicy.",
            ["connection refused|ECONNREFUSED"]        = "A downstream dependency is refusing connections. Check service health.",
            ["timeout|ETIMEDOUT|context deadline"]     = "Request timed out. Review slow queries or network latency.",
            ["SIGKILL|SIGSEGV|core dumped"]            = "Fatal signal received (possible native crash). Review native dependencies.",
            ["disk.*full|no space left"]               = "Disk full on host machine. Free space or expand volume.",
            ["permission denied|EACCES"]               = "File permission error. Check container user and volume mounts.",
            ["panic:"]                                 = "Application panic detected. Review stack trace in raw logs.",
            ["500 Internal Server Error|HTTP 500"]     = "Internal server error. Review application exception logs.",
            ["database.*error|SQL.*Error|ORA-"]        = "Database error. Check DB connectivity and query health.",
            ["certificate.*expired|SSL.*handshake"]   = "TLS / certificate issue. Renew or update certificates.",
        };

        foreach (var (pattern2, cause) in rules)
            if (Regex.IsMatch(sample, pattern2, RegexOptions.IgnoreCase))
                return cause;

        return $"Log pattern matched — review raw logs for root cause. (Pattern: {pattern})";
    }
}

public record IngestTraceRequest(
    Guid TenantId, Guid? ProjectId,
    string TraceId, string RootSpanId, string ServiceName, string? Environment,
    string? OperationName, long DurationMs, int SpanCount, int ErrorSpanCount,
    string? HttpMethod, string? HttpUrl, int? HttpStatusCode, string? ErrorMessage,
    string? SpansJson
);
public record CreateRuleRequest(
    Guid TenantId, Guid ProjectId, string Name, string Pattern,
    string Severity, bool TriggerAlert, bool SummarizeCrash
);
public record IngestCrashRequest(
    Guid TenantId, Guid ProjectId, string ServiceName, string Pattern, string? RawLogSample
);
public record ResolveCrashRequest(string? Resolution);
