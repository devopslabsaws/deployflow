using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace DeployFlow.API.Controllers;

/// <summary>Auto-scaling policy management and event history.</summary>
[Authorize]
[Route("api/scaling")]
public class AutoScalingController : BaseController
{
    private readonly ApplicationDbContext _db;

    public AutoScalingController(IMediator mediator, ApplicationDbContext db) : base(mediator)
    {
        _db = db;
    }

    // ── Policies ──────────────────────────────────────────────────────────────

    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicies([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var q = _db.ScalingPolicies.Where(p => !p.IsDeleted);
        if (projectId.HasValue) q = q.Where(p => p.ProjectId == projectId.Value);
        return Ok(await q.OrderBy(p => p.Name).ToListAsync(ct));
    }

    [HttpGet("policies/{id:guid}")]
    public async Task<IActionResult> GetPolicy(Guid id, CancellationToken ct)
    {
        var policy = await _db.ScalingPolicies.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        return policy is null ? NotFound() : Ok(policy);
    }

    [HttpPost("policies")]
    public async Task<IActionResult> CreatePolicy([FromBody] ScalingPolicyRequest req, CancellationToken ct)
    {
        var policy = new ScalingPolicy
        {
            TenantId = req.TenantId,
            ProjectId = req.ProjectId,
            ServerId = req.ServerId,
            Name = req.Name,
            ContainerName = req.ContainerName,
            HorizontalEnabled = req.HorizontalEnabled,
            MinReplicas = req.MinReplicas,
            MaxReplicas = req.MaxReplicas,
            CpuScaleUpThreshold = req.CpuScaleUpThreshold,
            CpuScaleDownThreshold = req.CpuScaleDownThreshold,
            MemoryScaleUpThreshold = req.MemoryScaleUpThreshold,
            MemoryScaleDownThreshold = req.MemoryScaleDownThreshold,
            ScaleCooldownSeconds = req.ScaleCooldownSeconds,
            VerticalEnabled = req.VerticalEnabled,
            CpuLimit = req.CpuLimit,
            MemoryLimit = req.MemoryLimit,
            ScaleToZeroEnabled = req.ScaleToZeroEnabled,
            ScaleToZeroAfterMinutes = req.ScaleToZeroAfterMinutes,
        };
        _db.ScalingPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);
        return Ok(policy);
    }

    [HttpPut("policies/{id:guid}")]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] ScalingPolicyRequest req, CancellationToken ct)
    {
        var policy = await _db.ScalingPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy is null) return NotFound();
        policy.Name = req.Name;
        policy.ContainerName = req.ContainerName;
        policy.HorizontalEnabled = req.HorizontalEnabled;
        policy.MinReplicas = req.MinReplicas;
        policy.MaxReplicas = req.MaxReplicas;
        policy.CpuScaleUpThreshold = req.CpuScaleUpThreshold;
        policy.CpuScaleDownThreshold = req.CpuScaleDownThreshold;
        policy.MemoryScaleUpThreshold = req.MemoryScaleUpThreshold;
        policy.MemoryScaleDownThreshold = req.MemoryScaleDownThreshold;
        policy.ScaleCooldownSeconds = req.ScaleCooldownSeconds;
        policy.VerticalEnabled = req.VerticalEnabled;
        policy.CpuLimit = req.CpuLimit;
        policy.MemoryLimit = req.MemoryLimit;
        policy.ScaleToZeroEnabled = req.ScaleToZeroEnabled;
        policy.ScaleToZeroAfterMinutes = req.ScaleToZeroAfterMinutes;
        policy.IsActive = req.IsActive;
        policy.Touch();
        await _db.SaveChangesAsync(ct);
        return Ok(policy);
    }

    [HttpDelete("policies/{id:guid}")]
    public async Task<IActionResult> DeletePolicy(Guid id, CancellationToken ct)
    {
        var policy = await _db.ScalingPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy is null) return NotFound();
        policy.SoftDelete();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Manually trigger a scale-up or scale-down action.</summary>
    [HttpPost("policies/{id:guid}/scale")]
    public async Task<IActionResult> ManualScale(Guid id, [FromBody] ManualScaleRequest req, CancellationToken ct)
    {
        var policy = await _db.ScalingPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (policy is null) return NotFound();

        int from = policy.CurrentReplicas;
        int to = Math.Clamp(req.Replicas, policy.MinReplicas, policy.MaxReplicas);
        policy.CurrentReplicas = to;
        policy.LastScaledAt = DateTime.UtcNow;
        policy.IsScaledToZero = to == 0;
        policy.LastScalingDirection = to > from ? ScalingDirection.Up : to < from ? ScalingDirection.Down : ScalingDirection.None;

        var ev = new ScalingEvent
        {
            TenantId = policy.TenantId,
            PolicyId = policy.Id,
            ProjectId = policy.ProjectId,
            Direction = policy.LastScalingDirection,
            Trigger = ScalingTrigger.Manual,
            FromReplicas = from,
            ToReplicas = to,
            Notes = req.Reason,
            Succeeded = true,
        };
        _db.ScalingEvents.Add(ev);
        await _db.SaveChangesAsync(ct);
        return Ok(new { fromReplicas = from, toReplicas = to, eventId = ev.Id });
    }

    // ── Events / history ──────────────────────────────────────────────────────

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] Guid? projectId, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var q = _db.ScalingEvents.AsQueryable();
        if (projectId.HasValue) q = q.Where(e => e.ProjectId == projectId.Value);
        var events = await q.OrderByDescending(e => e.CreatedAt).Take(limit).ToListAsync(ct);
        return Ok(events);
    }
}

public record ScalingPolicyRequest(
    Guid TenantId, Guid ProjectId, Guid? ServerId,
    string Name, string ContainerName,
    bool HorizontalEnabled, int MinReplicas, int MaxReplicas,
    int CpuScaleUpThreshold, int CpuScaleDownThreshold,
    int MemoryScaleUpThreshold, int MemoryScaleDownThreshold,
    int ScaleCooldownSeconds,
    bool VerticalEnabled, string? CpuLimit, string? MemoryLimit,
    bool ScaleToZeroEnabled, int ScaleToZeroAfterMinutes,
    bool IsActive = true
);

public record ManualScaleRequest(int Replicas, string? Reason);
