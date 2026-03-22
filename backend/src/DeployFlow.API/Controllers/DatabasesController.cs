using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Databases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/databases")]
public class DatabasesController : BaseController
{
    public DatabasesController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetDatabases(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? serverId = null,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetDatabasesQuery(page, pageSize, serverId), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetDatabaseByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDatabaseRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new CreateDatabaseCommand(
            request.Name, request.Engine, request.Version, request.DatabaseName,
            request.Username, request.Password, request.StorageGb, request.AutoBackup,
            request.BackupSchedule, request.ServerId), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteDatabaseCommand(id), ct));
}
