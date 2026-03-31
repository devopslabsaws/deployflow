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

    // ── Docker Hub ────────────────────────────────────────────────────────────

    [HttpGet("docker-hub")]
    public async Task<IActionResult> GetDockerHubStatus(CancellationToken ct)
        => ToResponse(await Mediator.Send(new GetDockerHubStatusQuery(), ct));

    [HttpPost("docker-hub")]
    public async Task<IActionResult> ConnectDockerHub([FromBody] ConnectDockerHubRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(new ConnectDockerHubCommand(req.Username, req.AccessToken), ct));

    [HttpDelete("docker-hub")]
    public async Task<IActionResult> DisconnectDockerHub(CancellationToken ct)
        => ToResponse(await Mediator.Send(new DisconnectDockerHubCommand(), ct));

    [HttpPost("docker-hub/verify")]
    public async Task<IActionResult> VerifyDockerHub([FromBody] ConnectDockerHubRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(new VerifyDockerHubCommand(req.Username, req.AccessToken), ct));

    // ── GitHub ────────────────────────────────────────────────────────────────

    [HttpGet("github")]
    public async Task<IActionResult> GetGitHubStatus(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new GetIntegrationStatusQuery("github", "GitHub", "username"), ct));

    [HttpPost("github")]
    public async Task<IActionResult> ConnectGitHub([FromBody] ConnectGitHubRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new ConnectGitHubCommand(req.Username, req.PersonalAccessToken), ct));

    [HttpDelete("github")]
    public async Task<IActionResult> DisconnectGitHub(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new DisconnectIntegrationCommand("github", "GitHub"), ct));

    // ── GitLab ────────────────────────────────────────────────────────────────

    [HttpGet("gitlab")]
    public async Task<IActionResult> GetGitLabStatus(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new GetIntegrationStatusQuery("gitlab", "GitLab", "username"), ct));

    [HttpPost("gitlab")]
    public async Task<IActionResult> ConnectGitLab([FromBody] ConnectGitLabRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new ConnectGitLabCommand(req.Username, req.PersonalAccessToken), ct));

    [HttpDelete("gitlab")]
    public async Task<IActionResult> DisconnectGitLab(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new DisconnectIntegrationCommand("gitlab", "GitLab"), ct));

    // ── Slack ─────────────────────────────────────────────────────────────────

    [HttpGet("slack")]
    public async Task<IActionResult> GetSlackStatus(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new GetIntegrationStatusQuery("slack", "Slack"), ct));

    [HttpPost("slack")]
    public async Task<IActionResult> ConnectSlack([FromBody] ConnectSlackRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(new ConnectSlackCommand(req.WebhookUrl), ct));

    [HttpDelete("slack")]
    public async Task<IActionResult> DisconnectSlack(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new DisconnectIntegrationCommand("slack", "Slack"), ct));

    // ── Microsoft Teams ───────────────────────────────────────────────────────

    [HttpGet("msteams")]
    public async Task<IActionResult> GetTeamsStatus(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new GetIntegrationStatusQuery("msteams", "Microsoft Teams"), ct));

    [HttpPost("msteams")]
    public async Task<IActionResult> ConnectTeams([FromBody] ConnectTeamsRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(new ConnectTeamsCommand(req.WebhookUrl), ct));

    [HttpDelete("msteams")]
    public async Task<IActionResult> DisconnectTeams(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new DisconnectIntegrationCommand("msteams", "Microsoft Teams"), ct));

    // ── AWS ───────────────────────────────────────────────────────────────────

    [HttpGet("aws")]
    public async Task<IActionResult> GetAwsStatus(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new GetIntegrationStatusQuery("aws", "AWS", "accessKeyId"), ct));

    [HttpPost("aws")]
    public async Task<IActionResult> ConnectAws([FromBody] ConnectAwsRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new ConnectAwsCommand(req.AccessKeyId, req.SecretAccessKey, req.Region), ct));

    [HttpDelete("aws")]
    public async Task<IActionResult> DisconnectAws(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new DisconnectIntegrationCommand("aws", "AWS"), ct));

    // ── Grafana ───────────────────────────────────────────────────────────────

    [HttpGet("grafana")]
    public async Task<IActionResult> GetGrafanaStatus(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new GetIntegrationStatusQuery("grafana", "Grafana", "url"), ct));

    [HttpPost("grafana")]
    public async Task<IActionResult> ConnectGrafana([FromBody] ConnectGrafanaRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new ConnectGrafanaCommand(req.Url, req.ApiToken), ct));

    [HttpDelete("grafana")]
    public async Task<IActionResult> DisconnectGrafana(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new DisconnectIntegrationCommand("grafana", "Grafana"), ct));

    // ── Cloudflare ────────────────────────────────────────────────────────────

    [HttpGet("cloudflare")]
    public async Task<IActionResult> GetCloudflareStatus(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new GetIntegrationStatusQuery("cloudflare", "Cloudflare", "zoneId"), ct));

    [HttpPost("cloudflare")]
    public async Task<IActionResult> ConnectCloudflare([FromBody] ConnectCloudflareRequest req, CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new ConnectCloudflareCommand(req.ApiToken, req.ZoneId), ct));

    [HttpDelete("cloudflare")]
    public async Task<IActionResult> DisconnectCloudflare(CancellationToken ct)
        => ToResponse(await Mediator.Send(
            new DisconnectIntegrationCommand("cloudflare", "Cloudflare"), ct));
}

public record ConnectDockerHubRequest(string Username, string AccessToken);
public record ConnectGitHubRequest(string Username, string PersonalAccessToken);
public record ConnectGitLabRequest(string Username, string PersonalAccessToken);
public record ConnectSlackRequest(string WebhookUrl);
public record ConnectTeamsRequest(string WebhookUrl);
public record ConnectAwsRequest(string AccessKeyId, string SecretAccessKey, string Region);
public record ConnectGrafanaRequest(string Url, string ApiToken);
public record ConnectCloudflareRequest(string ApiToken, string ZoneId);
