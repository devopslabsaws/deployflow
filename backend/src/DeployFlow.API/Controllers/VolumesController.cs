using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/volumes")]
public class VolumesController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public VolumesController(
        IMediator mediator,
        ApplicationDbContext db,
        ICurrentUser currentUser)
        : base(mediator)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? projectId = null, CancellationToken ct = default)
    {
        var query = _db.Volumes
            .AsNoTracking()
            .Where(v => v.TenantId == _currentUser.TenantId);

        if (projectId.HasValue)
            query = query.Where(v => v.DockerName == $"project:{projectId.Value}");

        var items = await query
            .OrderByDescending(v => v.UpdatedAt)
            .ToListAsync(ct);

        return Ok(items.Select(Map));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVolumeRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Volume name is required." });

        var normalized = req.Name.Trim();
        var exists = await _db.Volumes.AnyAsync(
            v => v.TenantId == _currentUser.TenantId && v.Name == normalized,
            ct);

        if (exists)
            return Conflict(new { error = "A volume with this name already exists." });

        var volume = new Volume
        {
            TenantId = _currentUser.TenantId,
            Name = normalized,
            Driver = string.IsNullOrWhiteSpace(req.Driver) ? "local" : req.Driver.Trim(),
            MountPath = string.IsNullOrWhiteSpace(req.MountPath) ? null : req.MountPath.Trim(),
            Status = VolumeStatus.Active,
            SizeBytes = 0,
            DockerName = req.ProjectId.HasValue ? $"project:{req.ProjectId.Value}" : null,
            LastUsedAt = null,
        };

        _db.Volumes.Add(volume);
        await _db.SaveChangesAsync(ct);

        return Ok(Map(volume));
    }

    [HttpPost("{id:guid}/attach")]
    public async Task<IActionResult> Attach(Guid id, [FromBody] AttachVolumeRequest req, CancellationToken ct = default)
    {
        var volume = await _db.Volumes.FirstOrDefaultAsync(
            v => v.Id == id && v.TenantId == _currentUser.TenantId,
            ct);

        if (volume is null) return NotFound(new { error = "Volume not found." });

        if (!req.ProjectId.HasValue && !req.ServiceId.HasValue)
            return BadRequest(new { error = "Either projectId or serviceId is required." });

        if (req.ProjectId.HasValue && req.ServiceId.HasValue)
            return BadRequest(new { error = "Specify only one attachment target." });

        volume.DockerName = req.ProjectId.HasValue
            ? $"project:{req.ProjectId.Value}"
            : $"service:{req.ServiceId!.Value}";

        if (!string.IsNullOrWhiteSpace(req.MountPath))
            volume.MountPath = req.MountPath.Trim();

        volume.Status = VolumeStatus.Active;
        volume.LastUsedAt = DateTime.UtcNow;
        volume.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(Map(volume));
    }

    [HttpPost("{id:guid}/detach")]
    public async Task<IActionResult> Detach(Guid id, CancellationToken ct = default)
    {
        var volume = await _db.Volumes.FirstOrDefaultAsync(
            v => v.Id == id && v.TenantId == _currentUser.TenantId,
            ct);

        if (volume is null) return NotFound(new { error = "Volume not found." });

        volume.DockerName = null;
        volume.Status = VolumeStatus.Inactive;
        volume.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(Map(volume));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var volume = await _db.Volumes.FirstOrDefaultAsync(
            v => v.Id == id && v.TenantId == _currentUser.TenantId,
            ct);

        if (volume is null) return NotFound(new { error = "Volume not found." });

        if (!string.IsNullOrWhiteSpace(volume.DockerName))
            return Conflict(new { error = "Volume is attached. Detach it before deleting." });

        volume.SoftDelete(_currentUser.UserId);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static object Map(Volume v)
    {
        Guid? projectId = null;
        if (!string.IsNullOrWhiteSpace(v.DockerName) && v.DockerName.StartsWith("project:", StringComparison.OrdinalIgnoreCase))
        {
            var raw = v.DockerName["project:".Length..];
            if (Guid.TryParse(raw, out var pid)) projectId = pid;
        }

        return new
        {
            v.Id,
            v.Name,
            status = v.Status.ToString().ToLowerInvariant(),
            v.Driver,
            v.MountPath,
            v.SizeBytes,
            projectId,
            v.CreatedAt,
            v.UpdatedAt,
            v.LastUsedAt,
            attachment = v.DockerName
        };
    }
}

public record CreateVolumeRequest(string Name, string? MountPath, string? Driver, Guid? ProjectId);
public record AttachVolumeRequest(Guid? ProjectId, Guid? ServiceId, string? MountPath);
