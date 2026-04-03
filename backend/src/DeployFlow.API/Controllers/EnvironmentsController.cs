using DeployFlow.Application.Features.Environments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/environments")]
public class EnvironmentsController : BaseController
{
    public EnvironmentsController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetEnvironments(CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetEnvironmentsQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> CreateEnvironment(
        [FromBody] CreateEnvironmentCommand command, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(command, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEnvironment(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteEnvironmentCommand(id), ct));
}
