using DeployFlow.Application.Features.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/services")]
[Tags("Services")]
public class ServicesController : BaseController
{
    public ServicesController(IMediator mediator) : base(mediator) { }

    /// <summary>Get all services, optionally filtered by project.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetServicesQuery(projectId), ct);
        return ToResponse(result);
    }

    /// <summary>Create a new service.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new CreateServiceCommand(request.ProjectId, request.Name, request.Type, request.Image, request.Tag), ct);
        return ToResponse(result);
    }

    /// <summary>Start a service.</summary>
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new StartServiceCommand(id), ct);
        return ToResponse(result);
    }

    /// <summary>Stop a service.</summary>
    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> Stop(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new StopServiceCommand(id), ct);
        return ToResponse(result);
    }

    /// <summary>Restart a service.</summary>
    [HttpPost("{id:guid}/restart")]
    public async Task<IActionResult> Restart(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new RestartServiceCommand(id), ct);
        return ToResponse(result);
    }

    /// <summary>Delete a service.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new DeleteServiceCommand(id), ct);
        return ToResponse(result);
    }
}

public record CreateServiceRequest(Guid ProjectId, string Name, string Type, string? Image = null, string? Tag = null);
