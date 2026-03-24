using DeployFlow.Application.Features.StackDetection;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/stack-detection")]
public class StackDetectionController : BaseController
{
    public StackDetectionController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Analyze a repository file manifest and return detected stack + generated Dockerfile.
    /// </summary>
    [HttpPost("detect")]
    public async Task<IActionResult> Detect(
        [FromBody] DetectStackRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(
            new DetectStackQuery(
                request.FileNames,
                request.PackageJsonContent,
                request.RequirementsTxtContent,
                request.ComposerJsonContent,
                request.GemfileContent,
                request.ProjectId), ct));

    /// <summary>
    /// Apply detected stack settings to a project (saves build/start commands, framework, port).
    /// </summary>
    [HttpPost("apply/{projectId:guid}")]
    public async Task<IActionResult> Apply(
        Guid projectId,
        [FromBody] ApplyStackRequest request,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(
            new ApplyStackDetectionCommand(
                projectId,
                request.Framework,
                request.BuildCommandOverride,
                request.StartCommandOverride,
                request.InstallCommandOverride,
                request.PortOverride), ct));
}

public record DetectStackRequest(
    List<string> FileNames,
    string? PackageJsonContent = null,
    string? RequirementsTxtContent = null,
    string? ComposerJsonContent = null,
    string? GemfileContent = null,
    Guid? ProjectId = null
);

public record ApplyStackRequest(
    DeployFlow.Application.Features.StackDetection.DetectedFramework Framework,
    string? BuildCommandOverride,
    string? StartCommandOverride,
    string? InstallCommandOverride,
    int? PortOverride
);
