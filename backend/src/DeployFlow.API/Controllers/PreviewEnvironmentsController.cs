using DeployFlow.Application.Features.PreviewEnvironments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/preview-environments")]
public class PreviewEnvironmentsController : BaseController
{
    public PreviewEnvironmentsController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId = null, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetPreviewEnvironmentsQuery(projectId), ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePreviewEnvironmentCommand command, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(command, ct));

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdatePreviewStatusRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new UpdatePreviewStatusCommand(id, request.Status, request.Url), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cleanup(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new CleanupPreviewEnvironmentCommand(id), ct));
}

public record UpdatePreviewStatusRequest(string Status, string? Url);
