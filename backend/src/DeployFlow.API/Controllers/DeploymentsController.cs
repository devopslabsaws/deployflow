using DeployFlow.API.Attributes;
using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Deployments.Commands;
using DeployFlow.Application.Features.Deployments.Queries;
using DeployFlow.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Route("api/deployments")]
[Authorize]
public class DeploymentsController : BaseController
{
    public DeploymentsController(IMediator mediator) : base(mediator) { }

    /// <summary>List deployments with optional filters.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? branch = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDeploymentsQuery(page, pageSize, projectId, status, branch), ct);
        return ToResponse(result);
    }

    /// <summary>Get a single deployment.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetDeploymentQuery(id), ct);
        return ToResponse(result);
    }

    /// <summary>Get paginated deployment logs.</summary>
    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> GetLogs(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDeploymentLogsQuery(id, page, pageSize), ct);
        return ToResponse(result);
    }

    /// <summary>Trigger a new deployment.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TriggerDeploymentRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new TriggerDeploymentCommand(
            request.ProjectId, request.Branch, request.CommitSha, request.Trigger), ct);

        if (!result.IsSuccess) return ToResponse(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Cancel a running deployment.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new CancelDeploymentCommand(id), ct);
        return ToResponse(result);
    }

    /// <summary>Roll back to a previous successful deployment.</summary>
    [HttpPost("{id:guid}/rollback")]
    public async Task<IActionResult> Rollback(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new RollbackDeploymentCommand(id), ct);
        if (!result.IsSuccess) return ToResponse(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // ─── Approval ──────────────────────────────────────────────────────────────

    /// <summary>Approve a pending deployment.</summary>
    [HttpPost("{id:guid}/approve")]
    [RequireResourcePermission(PermissionResource.Deployment, ResourceAction.Deploy)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new ApproveDeploymentCommand(id, request.Notes), ct);
        return ToResponse(result);
    }

    /// <summary>Reject a pending deployment.</summary>
    [HttpPost("{id:guid}/reject")]
    [RequireResourcePermission(PermissionResource.Deployment, ResourceAction.Deploy)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ApprovalRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new RejectDeploymentCommand(id, request.Notes), ct);
        return ToResponse(result);
    }

    // ─── Canary ────────────────────────────────────────────────────────────────

    /// <summary>Start a canary release for a healthy deployment.</summary>
    [HttpPost("{id:guid}/canary/start")]
    [RequireResourcePermission(PermissionResource.Deployment, ResourceAction.Deploy)]
    public async Task<IActionResult> StartCanary(Guid id, [FromBody] StartCanaryRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new StartCanaryCommand(id, request.TrafficPercent, request.StepDurationMinutes), ct);
        return ToResponse(result);
    }

    /// <summary>Promote a running canary to 100% traffic.</summary>
    [HttpPost("{id:guid}/canary/promote")]
    [RequireResourcePermission(PermissionResource.Deployment, ResourceAction.Deploy)]
    public async Task<IActionResult> PromoteCanary(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new PromoteCanaryCommand(id), ct);
        return ToResponse(result);
    }

    /// <summary>Abort a running canary and roll back traffic.</summary>
    [HttpPost("{id:guid}/canary/abort")]
    [RequireResourcePermission(PermissionResource.Deployment, ResourceAction.Deploy)]
    public async Task<IActionResult> AbortCanary(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new AbortCanaryCommand(id), ct);
        return ToResponse(result);
    }

    /// <summary>
    /// Trigger a zero-downtime Blue/Green deployment for a project.
    /// Deploys to the inactive slot, health-checks it, then switches traffic.
    /// </summary>
    [HttpPost("blue-green")]
    public async Task<IActionResult> BlueGreen([FromBody] BlueGreenDeployRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new BlueGreenDeployCommand(request.ProjectId), ct);
        if (!result.IsSuccess) return ToResponse(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }
}

public record BlueGreenDeployRequest(Guid ProjectId);
public record ApprovalRequest(string? Notes);
public record StartCanaryRequest(int TrafficPercent, int StepDurationMinutes = 10);
