using DeployFlow.Application.Common;
using DeployFlow.Application.Features.Deployments.Commands;
using DeployFlow.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

/// <summary>
/// REST API for build-related operations.
///
/// Endpoints:
///   POST  /api/projects/{id}/build          — trigger a manual build for a project
///   GET   /api/projects/{id}/detect-stack   — preview the detected tech stack
///   GET   /api/projects/{id}/dockerfile     — preview the auto-generated Dockerfile script
///   GET   /api/builds/{deploymentId}/status — return the current build/deployment status
/// </summary>
[Route("api")]
[Authorize]
public class BuildController : BaseController
{
    private readonly IUnitOfWork                    _uow;
    private readonly IDockerfileGeneratorService    _dockerfileGenerator;

    public BuildController(
        IMediator                   mediator,
        IUnitOfWork                 uow,
        IDockerfileGeneratorService dockerfileGenerator)
        : base(mediator)
    {
        _uow                  = uow;
        _dockerfileGenerator  = dockerfileGenerator;
    }

    // ── POST /api/projects/{id}/build ─────────────────────────────────────────

    /// <summary>
    /// Triggers a manual deployment for the given project.
    /// Equivalent to clicking "Deploy" in the UI.
    /// </summary>
    [HttpPost("projects/{id:guid}/build")]
    public async Task<IActionResult> TriggerBuild(
        Guid                id,
        [FromBody]          BuildTriggerRequest? request,
        CancellationToken   ct)
    {
        var result = await Mediator.Send(
            new TriggerDeploymentCommand(id, request?.Branch, request?.CommitSha, "manual"), ct);

        if (!result.IsSuccess) return ToResponse(result);
        return Accepted(new { deploymentId = result.Value!.Id, status = result.Value.Status });
    }

    // ── GET /api/projects/{id}/detect-stack ───────────────────────────────────

    /// <summary>
    /// Returns the auto-detection bash script that would run on the server,
    /// which shows exactly which stack DeployFlow would choose for this project.
    /// Useful for debugging before the first deploy.
    /// </summary>
    [HttpGet("projects/{id:guid}/detect-stack")]
    public async Task<IActionResult> DetectStack(Guid id, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(id, ct);
        if (project is null) return NotFound(new { message = "Project not found" });

        var script = _dockerfileGenerator.GenerateAutoDetectScript(project);

        // Parse out which stack labels appear in the script for a summary
        var stacks = new List<string>();
        foreach (var line in script.Split('\n'))
        {
            if (line.TrimStart().StartsWith("log \"", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("detected"))
            {
                var label = line.Trim()
                    .TrimStart('l', 'o', 'g', ' ', '"')
                    .TrimEnd('"')
                    .Trim();
                stacks.Add(label);
            }
        }

        return Ok(new
        {
            projectId   = id,
            projectName = project.Name,
            detectionScript = script,
            detectedStackLabels = stacks,
        });
    }

    // ── GET /api/projects/{id}/dockerfile ─────────────────────────────────────

    /// <summary>
    /// Returns the full auto-detect + Dockerfile generation bash script that
    /// DeployFlow would embed in the deploy pipeline for this project.
    /// If the project already has a <c>DockerfilePath</c> configured, returns that path instead.
    /// </summary>
    [HttpGet("projects/{id:guid}/dockerfile")]
    public async Task<IActionResult> PreviewDockerfile(Guid id, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(id, ct);
        if (project is null) return NotFound(new { message = "Project not found" });

        if (!string.IsNullOrEmpty(project.DockerfilePath))
            return Ok(new
            {
                projectId       = id,
                mode            = "explicit",
                dockerfilePath  = project.DockerfilePath,
                script          = (string?)null,
            });

        var script = _dockerfileGenerator.GenerateAutoDetectScript(project);

        return Ok(new
        {
            projectId   = id,
            mode        = "auto-detect",
            script,
        });
    }

    // ── GET /api/builds/{deploymentId}/status ─────────────────────────────────

    /// <summary>
    /// Returns the current status and basic info for a deployment (build run).
    /// </summary>
    [HttpGet("builds/{deploymentId:guid}/status")]
    public async Task<IActionResult> GetBuildStatus(Guid deploymentId, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(deploymentId, ct);
        if (deployment is null) return NotFound(new { message = "Deployment not found" });

        return Ok(new
        {
            deploymentId = deployment.Id,
            projectId    = deployment.ProjectId,
            status       = deployment.Status.ToString().ToLowerInvariant(),
            startedAt    = deployment.StartedAt,
            completedAt  = deployment.FinishedAt,
            publicUrl    = deployment.Url,
            errorMessage = deployment.ErrorMessage,
            duration     = deployment.DurationSeconds,
        });
    }
}

/// <summary>Request body for <c>POST /api/projects/{id}/build</c>.</summary>
public record BuildTriggerRequest(string? Branch, string? CommitSha);
