using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.EnvVariables;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/env-variables")]
public class EnvVariablesController : BaseController
{
    public EnvVariablesController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetEnvVars(
        [FromQuery] Guid? projectId = null,
        [FromQuery] Guid? serviceId = null,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetEnvVariablesQuery(projectId, serviceId), ct));

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertEnvVariableRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new UpsertEnvVariableCommand(
            request.Key, request.Value, request.IsSecret, request.Environment,
            request.ProjectId, null), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteEnvVariableCommand(id), ct));
}
