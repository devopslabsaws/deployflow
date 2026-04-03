using DeployFlow.Application.Features.Templates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/templates")]
public class TemplatesController : BaseController
{
    public TemplatesController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    [AllowAnonymous]  // Template gallery is public read
    public async Task<IActionResult> GetTemplates(
        [FromQuery] string? category = null, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetTemplatesQuery(category), ct));

    [HttpPost("{slug}/deploy")]
    public async Task<IActionResult> DeployTemplate(
        string slug,
        [FromBody] DeployTemplateRequest request,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(
            new DeployTemplateCommand(slug, request.ProjectId, request.ServerId, request.EnvOverrides, request.EnvironmentName),
            ct));
}
