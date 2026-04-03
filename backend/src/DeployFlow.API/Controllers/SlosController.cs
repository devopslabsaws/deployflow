using DeployFlow.Application.Features.Insights;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

/// <summary>Service Level Objectives — per-project SLO configuration and reports.</summary>
[Authorize]
[Route("api/slos")]
public class SlosController : BaseController
{
    public SlosController(IMediator mediator) : base(mediator) { }

    /// <summary>List all SLOs for the tenant, optionally filtered by project.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId = null, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetProjectSlosQuery(projectId), ct));

    /// <summary>Get a single SLO by project ID.</summary>
    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetProjectSlosQuery(projectId), ct));

    /// <summary>Create or update the SLO for a project.</summary>
    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> Upsert(
        Guid projectId,
        [FromBody] UpsertSloRequest req,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(
            new UpsertProjectSloCommand(
                projectId,
                req.UptimeTargetPercent,
                req.P95LatencyMs,
                req.ErrorRateBudgetPercent,
                req.WindowDays,
                req.IsEnabled), ct));

    /// <summary>Delete the SLO for a project.</summary>
    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteProjectSloCommand(projectId), ct));

    /// <summary>Get an SLO report with deploy event correlation for a project.</summary>
    [HttpGet("{projectId:guid}/report")]
    public async Task<IActionResult> Report(Guid projectId, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetSloReportQuery(projectId), ct));
}

public record UpsertSloRequest(
    double UptimeTargetPercent,
    int P95LatencyMs,
    double ErrorRateBudgetPercent,
    int WindowDays,
    bool IsEnabled);
