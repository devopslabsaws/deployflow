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
}

public record ChatRequest(string Message, Guid? DeploymentId);
