using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Containers;
using MediatR;

namespace DeployFlow.API.Controllers;

[Authorize]
[ApiController]
[Route("api/servers/{serverId}/containers")]
public class ContainersController : BaseController
{
    public ContainersController(IMediator mediator) : base(mediator)
    {
    }

    /// <summary>
    /// List all containers on a server
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ContainerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListContainers(string serverId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetContainersQuery(serverId), ct);
        return ToResponse(result);
    }

    /// <summary>
    /// Start a container on a server
    /// </summary>
    [HttpPost("{containerId}/start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> StartContainer(string serverId, string containerId, CancellationToken ct)
    {
        var result = await Mediator.Send(new StartContainerCommand(serverId, containerId), ct);
        return ToResponse(result);
    }

    /// <summary>
    /// Stop a container on a server
    /// </summary>
    [HttpPost("{containerId}/stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> StopContainer(string serverId, string containerId, CancellationToken ct)
    {
        var result = await Mediator.Send(new StopContainerCommand(serverId, containerId), ct);
        return ToResponse(result);
    }

    /// <summary>
    /// Restart a container on a server
    /// </summary>
    [HttpPost("{containerId}/restart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RestartContainer(string serverId, string containerId, CancellationToken ct)
    {
        var result = await Mediator.Send(new RestartContainerCommand(serverId, containerId), ct);
        return ToResponse(result);
    }

    /// <summary>
    /// Remove a container from a server
    /// </summary>
    [HttpDelete("{containerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveContainer(string serverId, string containerId, CancellationToken ct)
    {
        var result = await Mediator.Send(new RemoveContainerCommand(serverId, containerId), ct);
        return ToResponse(result);
    }

    /// <summary>
    /// Get logs for a container
    /// </summary>
    [HttpGet("{containerId}/logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetContainerLogs(string serverId, string containerId, [FromQuery] int? lines = 100, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetContainerLogsQuery(serverId, containerId, lines), ct);
        return ToResponse(result);
    }
}
