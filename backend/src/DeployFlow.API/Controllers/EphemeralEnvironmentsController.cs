using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace DeployFlow.API.Controllers;

/// <summary>Ephemeral PR preview environments with TTL management.</summary>
[Authorize]
[Route("api/ephemeral")]
public class EphemeralEnvironmentsController : BaseController
{
    private readonly ApplicationDbContext _db;

    public EphemeralEnvironmentsController(IMediator mediator, ApplicationDbContext db) : base(mediator) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] EphemeralStatus? status, CancellationToken ct)
    {
        var q = _db.EphemeralEnvironments.Where(e => !e.IsDeleted);
        if (projectId.HasValue) q = q.Where(e => e.ProjectId == projectId.Value);
        if (status.HasValue) q = q.Where(e => e.Status == status.Value);
        return Ok(await q.OrderByDescending(e => e.CreatedAt).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var env = await _db.EphemeralEnvironments.FirstOrDefaultAsync(e => e.Id == id, ct);
        return env is null ? NotFound() : Ok(env);
    }

    /// <summary>Create a new ephemeral environment for a PR.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEphemeralRequest req, CancellationToken ct)
    {
        var env = new EphemeralEnvironment
        {
            TenantId = req.TenantId,
            ProjectId = req.ProjectId,
            ServerId = req.ServerId,
            PrNumber = req.PrNumber,
            PrTitle = req.PrTitle,
            PrUrl = req.PrUrl,
            Branch = req.Branch,
            CommitSha = req.CommitSha,
            AuthorName = req.AuthorName,
            AuthorAvatarUrl = req.AuthorAvatarUrl,
            TtlHours = req.TtlHours > 0 ? req.TtlHours : 24,
            ExpiresAt = DateTime.UtcNow.AddHours(req.TtlHours > 0 ? req.TtlHours : 24),
            AssignedPort = req.AssignedPort,
            Status = EphemeralStatus.Pending,
        };
        _db.EphemeralEnvironments.Add(env);
        await _db.SaveChangesAsync(ct);
        return Ok(env);
    }

    /// <summary>Mark environment as provisioned with a live URL.</summary>
    [HttpPost("{id:guid}/provision")]
    public async Task<IActionResult> Provision(Guid id, [FromBody] ProvisionEphemeralRequest req, CancellationToken ct)
    {
        var env = await _db.EphemeralEnvironments.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (env is null) return NotFound();
        env.Status = EphemeralStatus.Running;
        env.PreviewUrl = req.PreviewUrl;
        env.ContainerId = req.ContainerId;
        env.ContainerName = req.ContainerName;
        env.ProvisionedAt = DateTime.UtcNow;
        env.LastActivityAt = DateTime.UtcNow;
        env.Touch();
        await _db.SaveChangesAsync(ct);
        return Ok(env);
    }

    /// <summary>Extend the TTL by N hours.</summary>
    [HttpPost("{id:guid}/extend")]
    public async Task<IActionResult> ExtendTtl(Guid id, [FromBody] ExtendTtlRequest req, CancellationToken ct)
    {
        var env = await _db.EphemeralEnvironments.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (env is null) return NotFound();
        env.ExpiresAt = (env.ExpiresAt ?? DateTime.UtcNow).AddHours(req.AdditionalHours);
        env.TtlHours += req.AdditionalHours;
        env.Touch();
        await _db.SaveChangesAsync(ct);
        return Ok(new { expiresAt = env.ExpiresAt });
    }

    /// <summary>Immediately destroy an ephemeral environment (PR merged / manual).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Destroy(Guid id, CancellationToken ct)
    {
        var env = await _db.EphemeralEnvironments.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (env is null) return NotFound();
        env.Status = EphemeralStatus.Destroyed;
        env.DestroyedAt = DateTime.UtcNow;
        env.SoftDelete();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>List only expired environments (TTL elapsed, not yet destroyed).</summary>
    [HttpGet("expired")]
    public async Task<IActionResult> GetExpired(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.EphemeralEnvironments
            .Where(e => !e.IsDeleted && e.ExpiresAt < now && e.Status != EphemeralStatus.Destroyed && e.Status != EphemeralStatus.Destroying)
            .ToListAsync(ct);
        return Ok(expired);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var all = await _db.EphemeralEnvironments.Where(e => !e.IsDeleted).ToListAsync(ct);
        return Ok(new
        {
            total = all.Count,
            running = all.Count(e => e.Status == EphemeralStatus.Running),
            pending = all.Count(e => e.Status == EphemeralStatus.Pending),
            expired = all.Count(e => e.IsExpired),
            destroyed = all.Count(e => e.Status == EphemeralStatus.Destroyed),
        });
    }
}

public record CreateEphemeralRequest(
    Guid TenantId, Guid ProjectId, Guid? ServerId,
    string PrNumber, string PrTitle, string PrUrl,
    string Branch, string CommitSha,
    string? AuthorName, string? AuthorAvatarUrl,
    int TtlHours, int AssignedPort
);
public record ProvisionEphemeralRequest(string PreviewUrl, string? ContainerId, string? ContainerName);
public record ExtendTtlRequest(int AdditionalHours);
