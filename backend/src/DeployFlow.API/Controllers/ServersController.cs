using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Servers.Commands;
using DeployFlow.Application.Features.Servers.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Route("api/servers")]
[Authorize]
public class ServersController : BaseController
{
    public ServersController(IMediator mediator) : base(mediator) { }

    /// <summary>List all servers.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? provider = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetServersQuery(page, pageSize, status, provider), ct);
        return ToResponse(result);
    }

    /// <summary>Get server details.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetServerQuery(id), ct);
        return ToResponse(result);
    }

    /// <summary>Get historical metrics for a server.</summary>
    [HttpGet("{id:guid}/metrics")]
    public async Task<IActionResult> GetMetrics(Guid id, [FromQuery] int hours = 24, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetServerMetricsQuery(id, hours), ct);
        return ToResponse(result);
    }

    /// <summary>Add a new server.</summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddServerRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new AddServerCommand(
            request.Name, request.IpAddress, request.SshPort, request.SshUser,
            request.SshKeyId, request.Provider, request.Region,
            request.CpuCores, request.MemoryGb, request.DiskGb), ct);

        if (!result.IsSuccess) return ToResponse(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Update server settings (name, SSH user/port/key, region).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServerRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new UpdateServerCommand(
            id, request.Name, request.SshKeyId, request.SshPort, request.SshUser, request.Region), ct);
        return ToResponse(result);
    }

    /// <summary>Test SSH connectivity to a server.</summary>
    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new TestServerConnectionCommand(id), ct);
        return ToResponse(result);
    }

    /// <summary>Execute a command on the server via SSH.</summary>
    [HttpPost("{id:guid}/exec")]
    public async Task<IActionResult> Exec(Guid id, [FromBody] ExecRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new ExecServerCommand(id, request.Command), ct);
        return ToResponse(result);
    }

    /// <summary>Remove a server.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new DeleteServerCommand(id), ct);
        return ToResponse(result);
    }
}

public record ExecRequest(string Command);
