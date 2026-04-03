using DeployFlow.Application.Features.AI;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

/// <summary>AI-powered deployment analysis and chat endpoints.</summary>
[Route("api/ai")]
[Authorize]
public class AiController : BaseController
{
    public AiController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Analyzes a deployment's logs using rule-based AI pattern matching.
    /// Returns diagnosis, root cause, actionable suggestions, and auto-fix options.
    /// </summary>
    [HttpGet("analyze/{deploymentId:guid}")]
    public async Task<IActionResult> Analyze(Guid deploymentId, CancellationToken ct)
    {
        var result = await Mediator.Send(new AnalyzeDeploymentQuery(deploymentId), ct);
        return ToResponse(result);
    }

    /// <summary>AI deployment assistant chat endpoint.</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new AiDeploymentChatCommand(request.Message, request.DeploymentId), ct);
        return ToResponse(result);
    }

    [HttpGet("incident-commander/{deploymentId:guid}")]
    public async Task<IActionResult> IncidentCommander(Guid deploymentId, CancellationToken ct)
        => ToResponse(await Mediator.Send(new GetAiIncidentCommanderQuery(deploymentId), ct));

    [HttpPost("risk-assessment")]
    public async Task<IActionResult> RiskAssessment([FromBody] RiskAssessmentRequest request, CancellationToken ct)
        => ToResponse(await Mediator.Send(new AssessDeploymentRiskCommand(request.ProjectId, request.Branch, request.EnvironmentSlug), ct));

    [HttpPost("policy-simulation")]
    public async Task<IActionResult> PolicySimulation([FromBody] PolicySimulationRequest request, CancellationToken ct)
        => ToResponse(await Mediator.Send(new SimulatePolicyFromPromptCommand(request.Prompt), ct));

    [HttpPost("preview-qa")]
    public async Task<IActionResult> PreviewQa([FromBody] PreviewQaRequest request, CancellationToken ct)
        => ToResponse(await Mediator.Send(new GeneratePreviewQaPlanCommand(request.ProjectId, request.Branch, request.PrTitle, request.ChangeSummary), ct));

    [HttpGet("pipeline-architect/{projectId:guid}")]
    public async Task<IActionResult> PipelineArchitect(Guid projectId, CancellationToken ct)
        => ToResponse(await Mediator.Send(new GeneratePipelineArchitectureQuery(projectId), ct));

    [HttpGet("ops-memory")]
    public async Task<IActionResult> OpsMemory([FromQuery] Guid? projectId, CancellationToken ct)
        => ToResponse(await Mediator.Send(new GetAiOpsMemoryQuery(projectId), ct));
}

public record ChatRequest(string Message, Guid? DeploymentId);
public record RiskAssessmentRequest(Guid ProjectId, string? Branch, string? EnvironmentSlug);
public record PolicySimulationRequest(string Prompt);
public record PreviewQaRequest(Guid ProjectId, string Branch, string PrTitle, string? ChangeSummary);
