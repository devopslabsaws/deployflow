using DeployFlow.Application.Features.Insights;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/insights")]
public class InsightsController : BaseController
{
    public InsightsController(IMediator mediator) : base(mediator) { }

    /// <summary>Deployment trends: success rate, avg duration, daily breakdown.</summary>
    [HttpGet("deployments")]
    public async Task<IActionResult> GetDeploymentInsights(
        [FromQuery] string period = "30d", CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetDeploymentInsightsQuery(period), ct));

    /// <summary>Smart error analysis for a specific deployment.</summary>
    [HttpGet("deployments/{deploymentId:guid}/errors")]
    public async Task<IActionResult> AnalyzeErrors(
        Guid deploymentId, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new AnalyzeDeploymentErrorsQuery(deploymentId), ct));

    /// <summary>Outbound webhooks: list, create, delete, test.</summary>
    [HttpGet("webhooks")]
    public async Task<IActionResult> GetWebhooks(
        [FromQuery] Guid? projectId = null, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetOutboundWebhooksQuery(projectId), ct));

    [HttpPost("webhooks")]
    public async Task<IActionResult> CreateWebhook(
        [FromBody] CreateOutboundWebhookCommand command, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(command, ct));

    [HttpDelete("webhooks/{id:guid}")]
    public async Task<IActionResult> DeleteWebhook(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteOutboundWebhookCommand(id), ct));

    [HttpPost("webhooks/{id:guid}/test")]
    public async Task<IActionResult> TestWebhook(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new TestOutboundWebhookCommand(id), ct));
}
