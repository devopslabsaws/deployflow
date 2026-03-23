using DeployFlow.Application.Common;
using DeployFlow.Application.Features.Deployments.Commands;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DeployFlow.API.Controllers;

/// <summary>
/// Manages deploy webhooks per project (secrets + inbound trigger endpoints).
/// Mirrors the Coolify Webhooks configuration screen.
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/webhooks")]
public class WebhooksController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public WebhooksController(IMediator mediator, ApplicationDbContext db, ICurrentUser currentUser)
        : base(mediator)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
    {
        var wh = await _db.DeployWebhooks
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && !w.IsDeleted, ct);

        if (wh is null)
        {
            // Auto-create on first access
            wh = DeployWebhook.Create(_currentUser.TenantId, projectId);
            _db.DeployWebhooks.Add(wh);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(ToDto(wh, Request));
    }

    [HttpPut("secrets")]
    public async Task<IActionResult> UpdateSecrets(
        Guid projectId,
        [FromBody] UpdateWebhookSecretsRequest req,
        CancellationToken ct)
    {
        var wh = await _db.DeployWebhooks
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && !w.IsDeleted, ct);

        if (wh is null)
        {
            wh = DeployWebhook.Create(_currentUser.TenantId, projectId);
            _db.DeployWebhooks.Add(wh);
        }

        wh.SetSecrets(req.GitHubSecret, req.GitLabSecret, req.BitbucketSecret, req.GiteaSecret);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(wh, Request));
    }

    private static object ToDto(DeployWebhook wh, HttpRequest req)
    {
        var baseUrl = $"{req.Scheme}://{req.Host}";
        return new
        {
            wh.Id,
            wh.ProjectId,
            wh.Token,
            wh.IsActive,
            DeployUrl   = $"{baseUrl}/api/webhooks/deploy/{wh.Token}",
            GitHubUrl   = $"{baseUrl}/api/webhooks/source/github/events/manual",
            GitLabUrl   = $"{baseUrl}/api/webhooks/source/gitlab/events/manual",
            BitbucketUrl= $"{baseUrl}/api/webhooks/source/bitbucket/events/manual",
            GiteaUrl    = $"{baseUrl}/api/webhooks/source/gitea/events/manual",
            HasGitHubSecret    = !string.IsNullOrEmpty(wh.GitHubSecret),
            HasGitLabSecret    = !string.IsNullOrEmpty(wh.GitLabSecret),
            HasBitbucketSecret = !string.IsNullOrEmpty(wh.BitbucketSecret),
            HasGiteaSecret     = !string.IsNullOrEmpty(wh.GiteaSecret),
        };
    }
}

/// <summary>
/// Inbound webhook receiver — handles signed pushes from GitHub, GitLab, Bitbucket, Gitea.
/// No [Authorize] because requests come from external services.
/// </summary>
[Route("api/webhooks")]
public class WebhookReceiverController : BaseController
{
    private readonly ApplicationDbContext _db;

    public WebhookReceiverController(IMediator mediator, ApplicationDbContext db)
        : base(mediator) { _db = db; }

    /// <summary>Authenticated deploy trigger (UUID token in URL).</summary>
    [HttpPost("deploy/{token}")]
    public async Task<IActionResult> DeployByToken(string token, CancellationToken ct)
    {
        var wh = await _db.DeployWebhooks
            .FirstOrDefaultAsync(w => w.Token == token && w.IsActive && !w.IsDeleted, ct);

        if (wh is null) return NotFound(new { error = "Webhook token not found." });

        var cmd = new TriggerDeploymentCommand(wh.ProjectId, null, null, "api");
        var result = await Mediator.Send(cmd, ct);
        return result.IsSuccess
            ? Ok(new { message = "Deployment triggered.", deploymentId = result.Value!.Id })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>GitHub push event receiver.</summary>
    [HttpPost("source/github/events/manual")]
    public async Task<IActionResult> GitHub(
        [FromHeader(Name = "X-GitHub-Event")] string? ev,
        [FromHeader(Name = "X-Hub-Signature-256")] string? sig,
        [FromQuery] Guid projectId,
        CancellationToken ct)
    {
        if (ev != "push" && ev != "workflow_dispatch")
            return Ok(new { message = "Event ignored." });

        var wh = await _db.DeployWebhooks
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.IsActive && !w.IsDeleted, ct);

        if (wh is null) return NotFound();

        if (!string.IsNullOrEmpty(wh.GitHubSecret) && !string.IsNullOrEmpty(sig) &&
            !await VerifyGitHubSignature(Request, wh.GitHubSecret, sig))
            return Unauthorized(new { error = "Invalid signature." });

        var result = await Mediator.Send(new TriggerDeploymentCommand(projectId, null, null, "push"), ct);
        return result.IsSuccess ? Ok(new { message = "Queued." }) : BadRequest(new { error = result.Error });
    }

    /// <summary>GitLab push event receiver.</summary>
    [HttpPost("source/gitlab/events/manual")]
    public async Task<IActionResult> GitLab(
        [FromHeader(Name = "X-Gitlab-Event")] string? ev,
        [FromHeader(Name = "X-Gitlab-Token")] string? token,
        [FromQuery] Guid projectId,
        CancellationToken ct)
    {
        var wh = await _db.DeployWebhooks
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.IsActive && !w.IsDeleted, ct);

        if (wh is null) return NotFound();

        if (!string.IsNullOrEmpty(wh.GitLabSecret) && token != wh.GitLabSecret)
            return Unauthorized(new { error = "Invalid token." });

        var result = await Mediator.Send(new TriggerDeploymentCommand(projectId, null, null, "push"), ct);
        return result.IsSuccess ? Ok(new { message = "Queued." }) : BadRequest(new { error = result.Error });
    }

    /// <summary>Bitbucket and Gitea stubs (same pattern — validate secret, trigger deploy).</summary>
    [HttpPost("source/bitbucket/events/manual")]
    [HttpPost("source/gitea/events/manual")]
    public async Task<IActionResult> GenericWebhook([FromQuery] Guid projectId, CancellationToken ct)
    {
        var wh = await _db.DeployWebhooks
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.IsActive && !w.IsDeleted, ct);

        if (wh is null) return NotFound();

        var result = await Mediator.Send(new TriggerDeploymentCommand(projectId, null, null, "push"), ct);
        return result.IsSuccess ? Ok(new { message = "Queued." }) : BadRequest(new { error = result.Error });
    }

    private static async Task<bool> VerifyGitHubSignature(HttpRequest request, string secret, string signature)
    {
        request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);

        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(body));
        var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }
}

public record UpdateWebhookSecretsRequest(
    string? GitHubSecret,
    string? GitLabSecret,
    string? BitbucketSecret,
    string? GiteaSecret);
