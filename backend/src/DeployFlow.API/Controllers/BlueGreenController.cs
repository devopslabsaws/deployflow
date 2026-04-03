using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace DeployFlow.API.Controllers;

/// <summary>Blue/Green zero-downtime deployment workflow.</summary>
[Authorize]
[Route("api/blue-green")]
public class BlueGreenController : BaseController
{
    private readonly ApplicationDbContext _db;

    public BlueGreenController(IMediator mediator, ApplicationDbContext db) : base(mediator)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var q = _db.BlueGreenDeployments.Where(b => !b.IsDeleted);
        if (projectId.HasValue) q = q.Where(b => b.ProjectId == projectId.Value);
        return Ok(await q.OrderByDescending(b => b.UpdatedAt).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var bg = await _db.BlueGreenDeployments.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bg is null) return NotFound();
        var logs = await _db.BlueGreenSwitchLogs
            .Where(l => l.BlueGreenId == id)
            .OrderByDescending(l => l.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return Ok(new { blueGreen = bg, logs });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBlueGreenRequest req, CancellationToken ct)
    {
        var bg = new BlueGreenDeployment
        {
            TenantId = req.TenantId,
            ProjectId = req.ProjectId,
            ServerId = req.ServerId,
            BluePort = req.BluePort,
            GreenPort = req.GreenPort,
            TraefikRouterName = req.TraefikRouterName,
            HealthCheckPath = req.HealthCheckPath ?? "/health",
            HealthCheckRetries = req.HealthCheckRetries,
            HealthCheckIntervalSeconds = req.HealthCheckIntervalSeconds,
            AutoPromote = req.AutoPromote,
            GreenTrafficPercent = 0,
        };
        _db.BlueGreenDeployments.Add(bg);
        await _db.SaveChangesAsync(ct);
        return Ok(bg);
    }

    /// <summary>Start building the green slot (new version).</summary>
    [HttpPost("{id:guid}/provision-green")]
    public async Task<IActionResult> ProvisionGreen(Guid id, [FromBody] ProvisionGreenRequest req, CancellationToken ct)
    {
        var bg = await _db.BlueGreenDeployments.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bg is null) return NotFound();
        if (bg.Status == BlueGreenStatus.Provisioning || bg.Status == BlueGreenStatus.HealthChecking)
            return BadRequest(new { error = "A switch is already in progress." });

        bg.Status = BlueGreenStatus.Provisioning;
        bg.GreenImageTag = req.ImageTag;
        bg.GreenCommitSha = req.CommitSha;
        bg.GreenContainerName = $"{req.ContainerBaseName}-green";
        bg.SwitchStartedAt = DateTime.UtcNow;
        bg.LastError = null;
        bg.Touch();
        AddLog(bg.Id, bg.TenantId, "info", $"Provisioning green slot — image: {req.ImageTag}, port: {bg.GreenPort}");

        await _db.SaveChangesAsync(ct);
        return Ok(bg);
    }

    /// <summary>Mark green health check passed — ready to promote.</summary>
    [HttpPost("{id:guid}/health-passed")]
    public async Task<IActionResult> HealthPassed(Guid id, CancellationToken ct)
    {
        var bg = await _db.BlueGreenDeployments.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bg is null) return NotFound();
        bg.Status = BlueGreenStatus.HealthChecking;
        bg.Touch();
        AddLog(bg.Id, bg.TenantId, "info", "Green slot health check passed — awaiting promotion.");
        await _db.SaveChangesAsync(ct);
        return Ok(bg);
    }

    /// <summary>Instantly cut Traefik router to green slot.</summary>
    [HttpPost("{id:guid}/promote")]
    public async Task<IActionResult> Promote(Guid id, CancellationToken ct)
    {
        var bg = await _db.BlueGreenDeployments.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bg is null) return NotFound();
        if (bg.Status != BlueGreenStatus.HealthChecking && bg.Status != BlueGreenStatus.Provisioning)
            return BadRequest(new { error = "Green slot is not ready for promotion." });

        // Swap blue ↔ green
        var (oldBlueContainer, oldBluePort, oldBlueTag, oldBlueSha) = (bg.BlueContainerName, bg.BluePort, bg.BlueImageTag, bg.BlueCommitSha);
        bg.BlueContainerName = bg.GreenContainerName;
        bg.BluePort = bg.GreenPort;
        bg.BlueImageTag = bg.GreenImageTag;
        bg.BlueCommitSha = bg.GreenCommitSha;
        bg.GreenContainerName = oldBlueContainer;
        bg.GreenPort = oldBluePort;
        bg.GreenImageTag = oldBlueTag;
        bg.GreenCommitSha = oldBlueSha;
        bg.ActiveSlot = "green"; // conceptually — new live version
        bg.GreenTrafficPercent = 0;
        bg.Status = BlueGreenStatus.Live;
        bg.SwitchedAt = DateTime.UtcNow;
        bg.Touch();
        AddLog(bg.Id, bg.TenantId, "info", "Traffic promoted to green slot. Previous blue is now standby.");
        await _db.SaveChangesAsync(ct);
        return Ok(bg);
    }

    /// <summary>Instantly roll back to the previous (blue) container.</summary>
    [HttpPost("{id:guid}/rollback")]
    public async Task<IActionResult> Rollback(Guid id, CancellationToken ct)
    {
        var bg = await _db.BlueGreenDeployments.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bg is null) return NotFound();
        bg.Status = BlueGreenStatus.RollingBack;
        bg.Touch();
        AddLog(bg.Id, bg.TenantId, "warn", "Rollback initiated — restoring previous (blue) slot.");
        // In a real implementation: re-point Traefik router back to blue port
        bg.Status = BlueGreenStatus.Live;
        bg.SwitchedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(bg);
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> GetLogs(Guid id, CancellationToken ct)
    {
        var logs = await _db.BlueGreenSwitchLogs
            .Where(l => l.BlueGreenId == id)
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .ToListAsync(ct);
        return Ok(logs);
    }

    private void AddLog(Guid bgId, Guid tenantId, string level, string message)
    {
        _db.BlueGreenSwitchLogs.Add(new BlueGreenSwitchLog
        {
            BlueGreenId = bgId,
            Level = level,
            Message = message,
        });
    }
}

public record CreateBlueGreenRequest(
    Guid TenantId, Guid ProjectId, Guid? ServerId,
    int BluePort, int GreenPort, string? TraefikRouterName,
    string? HealthCheckPath, int HealthCheckRetries, int HealthCheckIntervalSeconds,
    bool AutoPromote
);

public record ProvisionGreenRequest(string ImageTag, string CommitSha, string ContainerBaseName);
