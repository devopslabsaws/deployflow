using DeployFlow.Application.Features.Compose;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/compose")]
public class ComposeController : BaseController
{
    public ComposeController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetStacks(
        [FromQuery] Guid? projectId = null, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetComposeStacksQuery(projectId), ct));

    [HttpPost]
    public async Task<IActionResult> CreateStack(
        [FromBody] CreateComposeStackCommand command, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(command, ct));

    [HttpPost("{id:guid}/deploy")]
    public async Task<IActionResult> DeployStack(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeployComposeStackCommand(id), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStack(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteComposeStackCommand(id), ct));
}
