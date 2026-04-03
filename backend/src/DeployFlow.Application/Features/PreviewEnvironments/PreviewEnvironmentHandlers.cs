using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DeployFlow.Application.Features.PreviewEnvironments;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record PreviewEnvironmentDto(
    Guid Id,
    Guid ProjectId,
    string PrNumber,
    string PrTitle,
    string Branch,
    string? Url,
    string Status,
    Guid? DeploymentId,
    DateTime? MergedAt,
    DateTime? ClosedAt,
    DateTime CreatedAt
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetPreviewEnvironmentsQuery(Guid? ProjectId) : IRequest<Result<List<PreviewEnvironmentDto>>>;

public class GetPreviewEnvironmentsQueryHandler
    : IRequestHandler<GetPreviewEnvironmentsQuery, Result<List<PreviewEnvironmentDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetPreviewEnvironmentsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<PreviewEnvironmentDto>>> Handle(
        GetPreviewEnvironmentsQuery request, CancellationToken ct)
    {
        var previews = await _uow.PreviewEnvironments.GetByTenantAsync(_currentUser.TenantId, ct);

        var filtered = request.ProjectId.HasValue
            ? previews.Where(p => p.ProjectId == request.ProjectId.Value)
            : previews;

        return Result<List<PreviewEnvironmentDto>>.Success(
            filtered.OrderByDescending(p => p.CreatedAt).Select(Map).ToList());
    }

    internal static PreviewEnvironmentDto Map(PreviewEnvironment p) => new(
        p.Id, p.ProjectId, p.PrNumber, p.PrTitle, p.Branch,
        p.Url, p.Status, p.DeploymentId, p.MergedAt, p.ClosedAt, p.CreatedAt);
}

// ── Create (triggered by Git webhook on PR open/sync) ─────────────────────────

public record CreatePreviewEnvironmentCommand(
    Guid ProjectId,
    string PrNumber,
    string PrTitle,
    string Branch
) : IRequest<Result<PreviewEnvironmentDto>>;

public class CreatePreviewEnvironmentCommandHandler
    : IRequestHandler<CreatePreviewEnvironmentCommand, Result<PreviewEnvironmentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreatePreviewEnvironmentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<PreviewEnvironmentDto>> Handle(
        CreatePreviewEnvironmentCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<PreviewEnvironmentDto>.Failure("Project not found.", "404");

        // Check for existing preview for same PR
        var existing = (await _uow.PreviewEnvironments
            .GetByTenantAsync(_currentUser.TenantId, ct))
            .FirstOrDefault(p => p.ProjectId == request.ProjectId && p.PrNumber == request.PrNumber);

        if (existing is not null)
        {
            // Update to "building" on re-push
            existing.Status = "building";
            await _uow.PreviewEnvironments.UpdateAsync(existing, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<PreviewEnvironmentDto>.Success(GetPreviewEnvironmentsQueryHandler.Map(existing));
        }

        // Generate deterministic preview URL from branch slug
        var branchSlug = request.Branch
            .ToLowerInvariant()
            .Replace('/', '-')
            .Replace('_', '-');
            // Derive preview URL from server IP when available
            string previewUrl;
            if (project.ServerId.HasValue)
            {
                var server = await _uow.Servers.GetByIdAsync(project.ServerId.Value, ct);
                if (server is not null)
                {
                    // Assign a deterministic port in range 20000-29999 derived from PR number
                    var prNum = int.TryParse(request.PrNumber, out var n) ? n : 0;
                    var port  = 20000 + (prNum % 10000);
                    previewUrl = $"http://{server.IpAddress}:{port}";
                }
                else
                {
                    var slug = new string(branchSlug.Take(20).ToArray());
                    previewUrl = $"https://pr-{request.PrNumber}-{slug}.preview.local";
                }
            }
            else
            {
                var slug = new string(branchSlug.Take(20).ToArray());
                previewUrl = $"https://pr-{request.PrNumber}-{slug}.preview.local";
            }

        var preview = new PreviewEnvironment
        {
            TenantId = _currentUser.TenantId,
            ProjectId = request.ProjectId,
            PrNumber = request.PrNumber,
            PrTitle = request.PrTitle,
            Branch = request.Branch,
            Url = previewUrl,
            Status = "building"
        };

        await _uow.PreviewEnvironments.AddAsync(preview, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<PreviewEnvironmentDto>.Success(GetPreviewEnvironmentsQueryHandler.Map(preview));
    }
}

// ── Update Status ─────────────────────────────────────────────────────────────

public record UpdatePreviewStatusCommand(Guid Id, string Status, string? Url) : IRequest<Result<bool>>;

public class UpdatePreviewStatusCommandHandler : IRequestHandler<UpdatePreviewStatusCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdatePreviewStatusCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdatePreviewStatusCommand request, CancellationToken ct)
    {
        var preview = await _uow.PreviewEnvironments.GetByIdAsync(request.Id, ct);
        if (preview is null || preview.TenantId != _currentUser.TenantId)
            return Result<bool>.Failure("Preview environment not found.", "404");

        preview.Status = request.Status;
        if (request.Url is not null)
            preview.Url = request.Url;

        if (request.Status is "merged")
            preview.MergedAt = DateTime.UtcNow;
        else if (request.Status is "closed")
            preview.ClosedAt = DateTime.UtcNow;

        await _uow.PreviewEnvironments.UpdateAsync(preview, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ── Post PR/MR comment with preview URL ──────────────────────────────────────

/// <summary>
/// Posts a comment to a GitHub PR or GitLab MR with the preview environment URL.
/// </summary>
/// <param name="RepoOwner">GitHub owner or GitLab namespace (e.g. "acme").</param>
/// <param name="RepoName">Repository name (e.g. "api").</param>
/// <param name="PrNumber">Pull/Merge request number.</param>
/// <param name="PreviewUrl">The live preview URL to advertise.</param>
/// <param name="ApiToken">Personal access token with repo comment permissions.</param>
/// <param name="Provider">"github" or "gitlab".</param>
public record PostPrCommentCommand(
    string RepoOwner,
    string RepoName,
    string PrNumber,
    string PreviewUrl,
    string ApiToken,
    string Provider = "github"
) : IRequest<Result<bool>>;

public class PostPrCommentCommandHandler : IRequestHandler<PostPrCommentCommand, Result<bool>>
{
    private readonly IHttpClientFactory _httpClientFactory;

    public PostPrCommentCommandHandler(IHttpClientFactory httpClientFactory)
        => _httpClientFactory = httpClientFactory;

    public async Task<Result<bool>> Handle(PostPrCommentCommand request, CancellationToken ct)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DeployFlow/1.0");

            var body = $"""
                ### 🚀 Preview Environment Ready

                A preview deployment was created for this pull request.

                **URL:** {request.PreviewUrl}

                > Automatically deployed by DeployFlow. Environment will be cleaned up when the PR is closed.
                """;

            string requestUri;
            string mediaType = "application/json";

            if (request.Provider.Equals("gitlab", StringComparison.OrdinalIgnoreCase))
            {
                // GitLab: POST /projects/:id/merge_requests/:mr_iid/notes
                var encodedProject = Uri.EscapeDataString($"{request.RepoOwner}/{request.RepoName}");
                requestUri = $"https://gitlab.com/api/v4/projects/{encodedProject}/merge_requests/{request.PrNumber}/notes";
                client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", request.ApiToken);
                var payload = JsonSerializer.Serialize(new { body });
                using var content = new StringContent(payload, Encoding.UTF8, mediaType);
                var resp = await client.PostAsync(requestUri, content, ct);
                return resp.IsSuccessStatusCode
                    ? Result<bool>.Success(true)
                    : Result<bool>.Failure($"GitLab API error: {resp.StatusCode}");
            }
            else
            {
                // GitHub: POST /repos/:owner/:repo/issues/:number/comments
                requestUri = $"https://api.github.com/repos/{request.RepoOwner}/{request.RepoName}/issues/{request.PrNumber}/comments";
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", request.ApiToken);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                var payload = JsonSerializer.Serialize(new { body });
                using var content = new StringContent(payload, Encoding.UTF8, mediaType);
                var resp = await client.PostAsync(requestUri, content, ct);
                return resp.IsSuccessStatusCode
                    ? Result<bool>.Success(true)
                    : Result<bool>.Failure($"GitHub API error: {resp.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Failed to post PR comment: {ex.Message}");
        }
    }
}

// ── Cleanup after PR merge/close ─────────────────────────────────────────────

public record CleanupPreviewEnvironmentCommand(Guid Id) : IRequest<Result<bool>>;

public class CleanupPreviewEnvironmentCommandHandler
    : IRequestHandler<CleanupPreviewEnvironmentCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CleanupPreviewEnvironmentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(CleanupPreviewEnvironmentCommand request, CancellationToken ct)
    {
        var preview = await _uow.PreviewEnvironments.GetByIdAsync(request.Id, ct);
        if (preview is null || preview.TenantId != _currentUser.TenantId)
            return Result<bool>.Failure("Preview environment not found.", "404");

        await _uow.PreviewEnvironments.DeleteAsync(preview, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
