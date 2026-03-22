using DeployFlow.Application.Features.Integrations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Route("api/integrations")]
[Authorize]
public class IntegrationsController : BaseController
{
    public IntegrationsController(IMediator mediator) : base(mediator) { }

    /// <summary>Get Docker Hub integration status.</summary>
    [HttpGet("docker-hub")]
    public async Task<IActionResult> GetDockerHubStatus(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetDockerHubStatusQuery(), ct);
        return ToResponse(result);
    }

    /// <summary>Connect Docker Hub credentials.</summary>
    [HttpPost("docker-hub")]
    public async Task<IActionResult> ConnectDockerHub([FromBody] ConnectDockerHubRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new ConnectDockerHubCommand(request.Username, request.AccessToken), ct);
        return ToResponse(result);
    }

    /// <summary>Disconnect Docker Hub.</summary>
    [HttpDelete("docker-hub")]
    public async Task<IActionResult> DisconnectDockerHub(CancellationToken ct)
    {
        var result = await Mediator.Send(new DisconnectDockerHubCommand(), ct);
        return ToResponse(result);
    }

    /// <summary>Verify Docker Hub credentials without saving.</summary>
    [HttpPost("docker-hub/verify")]
    public async Task<IActionResult> VerifyDockerHub([FromBody] ConnectDockerHubRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new VerifyDockerHubCommand(request.Username, request.AccessToken), ct);
        return ToResponse(result);
    }

    /// <summary>Connect Slack webhook.</summary>
    [HttpPost("slack")]
    public async Task<IActionResult> ConnectSlack([FromBody] ConnectSlackRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new ConnectSlackCommand(request.WebhookUrl), ct);
        return ToResponse(result);
    }

    /// <summary>Connect AWS credentials.</summary>
    [HttpPost("aws")]
    public async Task<IActionResult> ConnectAws([FromBody] ConnectAwsRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new ConnectAwsCommand(request.AccessKeyId, request.SecretAccessKey, request.Region), ct);
        return ToResponse(result);
    }

    /// <summary>Connect Grafana.</summary>
    [HttpPost("grafana")]
    public async Task<IActionResult> ConnectGrafana([FromBody] ConnectGrafanaRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new ConnectGrafanaCommand(request.Url, request.ApiToken), ct);
        return ToResponse(result);
    }
}

public record ConnectDockerHubRequest(string Username, string AccessToken);
public record ConnectSlackRequest(string WebhookUrl);
public record ConnectAwsRequest(string AccessKeyId, string SecretAccessKey, string Region);
public record ConnectGrafanaRequest(string Url, string ApiToken);
