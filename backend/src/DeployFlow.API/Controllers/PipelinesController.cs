using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Pipelines;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/pipelines")]
public class PipelinesController : BaseController
{
    public PipelinesController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetPipelines(
        [FromQuery] Guid? projectId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetPipelinesQuery(projectId, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetPipelineByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePipelineRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new CreatePipelineCommand(
            request.Name, request.Description, request.ProjectId,
            request.Trigger, request.CronExpression), ct));

    [HttpPost("{id:guid}/trigger")]
    public async Task<IActionResult> Trigger(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new TriggerPipelineCommand(id), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeletePipelineCommand(id), ct));
}

public record CreatePipelineRequest(
    string Name,
    string? Description,
    Guid ProjectId,
    string Trigger,
    string? CronExpression
);
