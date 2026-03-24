using DeployFlow.Application.Features.Traefik;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/traefik")]
public class TraefikController : BaseController
{
    public TraefikController(IMediator mediator) : base(mediator) { }

    [HttpGet("routers")]
    public async Task<IActionResult> GetRouters(
        [FromQuery] Guid? serverId = null, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetTraefikRoutersQuery(serverId), ct));

    [HttpPost("routers")]
    public async Task<IActionResult> CreateRouter(
        [FromBody] CreateTraefikRouterCommand command, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(command, ct));

    [HttpPatch("routers/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleRouter(
        Guid id, [FromBody] ToggleRouterRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new ToggleTraefikRouterCommand(id, request.IsEnabled), ct));

    [HttpDelete("routers/{id:guid}")]
    public async Task<IActionResult> DeleteRouter(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteTraefikRouterCommand(id), ct));
}

public record ToggleRouterRequest(bool IsEnabled);
