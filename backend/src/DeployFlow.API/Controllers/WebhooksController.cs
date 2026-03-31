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

        // Buffer body for signature verification and payload extraction
        Request.EnableBuffering();
        var body = await ReadBodyAsync(Request);

        if (!string.IsNullOrEmpty(wh.GitHubSecret) && !string.IsNullOrEmpty(sig) &&
            !VerifyHmacSha256Signature(wh.GitHubSecret, body, sig, "sha256="))
            return Unauthorized(new { error = "Invalid signature." });

        var (branch, commitSha) = ExtractGitPushInfo(body);
        var result = await Mediator.Send(new TriggerDeploymentCommand(projectId, branch, commitSha, "push"), ct);
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

        Request.EnableBuffering();
        var body = await ReadBodyAsync(Request);
        // GitLab push payload: {"ref":"refs/heads/main","checkout_sha":"abc123"}
        var (branch, commitSha) = ExtractGitPushInfo(body, checkoutShaField: "checkout_sha");
        var result = await Mediator.Send(new TriggerDeploymentCommand(projectId, branch, commitSha, "push"), ct);
        return result.IsSuccess ? Ok(new { message = "Queued." }) : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Bitbucket Cloud push event receiver.
    /// Verifies HMAC-SHA256 signature via <c>X-Hub-Signature-256</c> header when a secret is configured.
    /// </summary>
    [HttpPost("source/bitbucket/events/manual")]
    public async Task<IActionResult> Bitbucket(
        [FromHeader(Name = "X-Event-Key")] string? ev,
        [FromHeader(Name = "X-Hub-Signature-256")] string? sig,
        [FromQuery] Guid projectId,
        CancellationToken ct)
    {
        if (ev is not null && !ev.StartsWith("repo:push", StringComparison.OrdinalIgnoreCase))
            return Ok(new { message = "Event ignored." });

        var wh = await _db.DeployWebhooks
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.IsActive && !w.IsDeleted, ct);

        if (wh is null) return NotFound();

        Request.EnableBuffering();
        var body = await ReadBodyAsync(Request);

        if (!string.IsNullOrEmpty(wh.BitbucketSecret) && !string.IsNullOrEmpty(sig) &&
            !VerifyHmacSha256Signature(wh.BitbucketSecret, body, sig, "sha256="))
            return Unauthorized(new { error = "Invalid signature." });

        // Bitbucket Cloud push payload:
        // {"push":{"changes":[{"new":{"name":"main","type":"branch","target":{"hash":"abc123"}}}]}}
        string? branch = null;
        string? commitSha = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("push", out var push) &&
                push.TryGetProperty("changes", out var changes) &&
                changes.GetArrayLength() > 0)
            {
                var first = changes[0];
                if (first.TryGetProperty("new", out var newBranch))
                {
                    branch = newBranch.TryGetProperty("name", out var name) ? name.GetString() : null;
                    if (newBranch.TryGetProperty("target", out var target) &&
                        target.TryGetProperty("hash", out var hash))
                        commitSha = hash.GetString();
                }
            }
        }
        catch { /* non-critical — proceed without branch info */ }

        var result = await Mediator.Send(new TriggerDeploymentCommand(projectId, branch, commitSha, "push"), ct);
        return result.IsSuccess ? Ok(new { message = "Queued." }) : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Gitea push event receiver.
    /// Verifies HMAC-SHA256 signature via <c>X-Gitea-Signature</c> header when a secret is configured.
    /// </summary>
    [HttpPost("source/gitea/events/manual")]
    public async Task<IActionResult> Gitea(
        [FromHeader(Name = "X-Gitea-Event")] string? ev,
        [FromHeader(Name = "X-Gitea-Signature")] string? sig,
        [FromQuery] Guid projectId,
        CancellationToken ct)
    {
        if (ev is not null && ev != "push")
            return Ok(new { message = "Event ignored." });

        var wh = await _db.DeployWebhooks
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.IsActive && !w.IsDeleted, ct);

        if (wh is null) return NotFound();

        Request.EnableBuffering();
        var body = await ReadBodyAsync(Request);

        // Gitea uses plain hex HMAC (no "sha256=" prefix)
        if (!string.IsNullOrEmpty(wh.GiteaSecret) && !string.IsNullOrEmpty(sig) &&
            !VerifyHmacSha256Signature(wh.GiteaSecret, body, sig, prefix: null))
            return Unauthorized(new { error = "Invalid signature." });

        // Gitea push payload: {"ref":"refs/heads/main","head_commit":{"id":"abc123"}}
        // Also supports: {"after":"abc123"}
        var (branch, commitSha) = ExtractGitPushInfo(body, headCommitIdField: "id");
        var result = await Mediator.Send(new TriggerDeploymentCommand(projectId, branch, commitSha, "push"), ct);
        return result.IsSuccess ? Ok(new { message = "Queued." }) : BadRequest(new { error = result.Error });
    }

    // ── Signature helpers ─────────────────────────────────────────────────────

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }

    /// <summary>
    /// Verifies an HMAC-SHA256 signature against the request body.
    /// The <paramref name="prefix"/> (e.g. "sha256=") is stripped before comparison when provided.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    private static bool VerifyHmacSha256Signature(string secret, string body, string signature, string? prefix)
    {
        var key   = Encoding.UTF8.GetBytes(secret);
        var hash  = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(body));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();

        var incoming = prefix is not null && signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? signature[prefix.Length..].ToLowerInvariant()
            : signature.ToLowerInvariant();

        // Pad to same length before FixedTimeEquals (secrets must be same length for timing safety)
        if (computed.Length != incoming.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(incoming));
    }

    /// <summary>
    /// Extracts branch name and commit SHA from a git push JSON payload.
    /// Handles GitHub/Gitea format: <c>{"ref":"refs/heads/main","head_commit":{"id":"abc"}}</c>
    /// and GitLab format: <c>{"ref":"refs/heads/main","checkout_sha":"abc"}</c>
    /// </summary>
    private static (string? Branch, string? CommitSha) ExtractGitPushInfo(
        string body,
        string? checkoutShaField = null,
        string  headCommitIdField = "id")
    {
        string? branch    = null;
        string? commitSha = null;
        try
        {
            using var doc  = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Ref → branch name
            if (root.TryGetProperty("ref", out var refProp))
            {
                var refStr = refProp.GetString() ?? "";
                branch = refStr.StartsWith("refs/heads/", StringComparison.Ordinal)
                    ? refStr["refs/heads/".Length..]
                    : refStr;
            }

            // Commit SHA — try multiple field names
            if (checkoutShaField is not null && root.TryGetProperty(checkoutShaField, out var csSha))
                commitSha = csSha.GetString();
            else if (root.TryGetProperty("head_commit", out var hc) &&
                     hc.TryGetProperty(headCommitIdField, out var hcId))
                commitSha = hcId.GetString();
            else if (root.TryGetProperty("after", out var after))
                commitSha = after.GetString();
        }
        catch { /* non-critical */ }
        return (branch, commitSha);
    }
}

public record UpdateWebhookSecretsRequest(
    string? GitHubSecret,
    string? GitLabSecret,
    string? BitbucketSecret,
    string? GiteaSecret);
