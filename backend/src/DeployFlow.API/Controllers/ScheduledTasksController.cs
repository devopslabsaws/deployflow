using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.API.Controllers;

/// <summary>
/// Manages and manually triggers scheduled tasks per project.
/// Matches the Coolify "Scheduled Tasks" configuration screen.
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/scheduled-tasks")]
public class ScheduledTasksController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ISshService _ssh;
    private readonly IEncryptionService _enc;
    private readonly IUnitOfWork _uow;

    public ScheduledTasksController(
        IMediator mediator,
        ApplicationDbContext db,
        ICurrentUser currentUser,
        ISshService ssh,
        IEncryptionService enc,
        IUnitOfWork uow)
        : base(mediator)
    {
        _db = db;
        _currentUser = currentUser;
        _ssh = ssh;
        _enc = enc;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId, CancellationToken ct)
    {
        var tasks = await _db.ScheduledTasks
            .Where(t => t.ProjectId == projectId && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return Ok(tasks.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var task = await _db.ScheduledTasks.FindAsync([id], ct);
        if (task is null || task.IsDeleted) return NotFound();
        return Ok(ToDto(task));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateScheduledTaskRequest req,
        CancellationToken ct)
    {
        var task = ScheduledTask.Create(
            _currentUser.TenantId, projectId,
            req.Name, req.Command, req.Frequency ?? "0 0 * * *",
            req.TimeoutSeconds ?? 300, req.ContainerName);

        _db.ScheduledTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { projectId, id = task.Id }, ToDto(task));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateScheduledTaskRequest req,
        CancellationToken ct)
    {
        var task = await _db.ScheduledTasks.FindAsync([id], ct);
        if (task is null || task.IsDeleted) return NotFound();

        task.Update(req.Name, req.Command, req.Frequency, req.TimeoutSeconds, req.ContainerName);

        if (req.IsActive.HasValue)
        {
            if (req.IsActive.Value) task.Activate();
            else task.Deactivate();
        }

        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(task));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var task = await _db.ScheduledTasks.FindAsync([id], ct);
        if (task is null || task.IsDeleted) return NotFound();
        task.SoftDelete();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Trigger a scheduled task immediately (manual run).</summary>
    [HttpPost("{id:guid}/run")]
    public async Task<IActionResult> RunNow(Guid projectId, Guid id, CancellationToken ct)
    {
        var task = await _db.ScheduledTasks.FindAsync([id], ct);
        if (task is null || task.IsDeleted) return NotFound();

        var project = await _uow.Projects.GetByIdAsync(projectId, ct);
        if (project?.ServerId is null)
            return BadRequest(new { error = "Project has no server assigned." });

        var server = await _uow.Servers.GetByIdAsync(project.ServerId.Value, ct);
        if (server is null) return BadRequest(new { error = "Server not found." });

        var sshKey = server.SshKeyId.HasValue
            ? await _uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct)
            : null;

        if (sshKey is null)
            return BadRequest(new { error = "No SSH key on server." });

        var privateKey = _enc.Decrypt(sshKey.PrivateKeyEncrypted);
        var containerFlag = string.IsNullOrEmpty(task.ContainerName)
            ? ""
            : $"docker exec {task.ContainerName} ";

        var script = $"#!/bin/bash\ntimeout {task.TimeoutSeconds} {containerFlag}{task.Command}";

        var result = await _ssh.ExecuteCommandAsync(
            server.IpAddress, server.SshPort, server.SshUser, privateKey, script, ct);

        var status = result.ExitCode == 0 ? "success" : result.ExitCode == 124 ? "timeout" : "failed";
        task.RecordRun(status, result.StdOut + result.StdErr);
        await _db.SaveChangesAsync(ct);

        return Ok(new { status, output = result.StdOut + result.StdErr, exitCode = result.ExitCode });
    }

    private static object ToDto(ScheduledTask t) => new
    {
        t.Id, t.ProjectId, t.Name, t.Command, t.Frequency,
        t.TimeoutSeconds, t.ContainerName, t.IsActive,
        t.LastRunAt, t.LastRunStatus, t.LastRunOutput,
        t.CreatedAt, t.UpdatedAt,
    };
}

public record CreateScheduledTaskRequest(
    string Name,
    string Command,
    string? Frequency,
    int? TimeoutSeconds,
    string? ContainerName);

public record UpdateScheduledTaskRequest(
    string? Name,
    string? Command,
    string? Frequency,
    int? TimeoutSeconds,
    string? ContainerName,
    bool? IsActive);
