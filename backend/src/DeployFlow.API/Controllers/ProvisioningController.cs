using DeployFlow.Application.Features.Provisioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/provisioning")]
public class ProvisioningController : BaseController
{
    public ProvisioningController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> ListJobs(CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new ListProvisioningJobsQuery(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetProvisioningJobQuery(id), ct));

    /// <summary>
    /// Plan a new server provisioning job (generates cloud-init + cost estimate).
    /// </summary>
    [HttpPost("plan")]
    public async Task<IActionResult> Plan(
        [FromBody] PlanServerRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(
            new PlanServerProvisioningCommand(
                request.Name, request.Provider, request.Region, request.Size,
                request.Os, request.SshKeyId, request.Tags),
            ct));

    /// <summary>
    /// Apply a planned provisioning job — triggers actual server creation with the provider API.
    /// </summary>
    [HttpPost("{id:guid}/apply")]
    public async Task<IActionResult> Apply(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new ApplyServerProvisioningCommand(id), ct));
}
