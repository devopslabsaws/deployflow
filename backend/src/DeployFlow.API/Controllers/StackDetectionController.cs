using DeployFlow.Application.Features.StackDetection;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

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
    /// Detect the tech stack of a remote repository by fetching its root file list via
    /// the GitHub or GitLab API. Returns Dockerfile + build/start commands.
    /// </summary>
    [HttpPost("detect-from-url")]
    public async Task<IActionResult> DetectFromUrl(
        [FromBody] DetectFromUrlRequest request,
        [FromServices] IHttpClientFactory httpClientFactory,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryUrl))
            return BadRequest(new { error = "repositoryUrl is required" });

        try
        {
            var (fileNames, pkgJson, reqsTxt, composerJson, gemfile) =
                await FetchRepoContentsAsync(
                    httpClientFactory,
                    request.RepositoryUrl.Trim(),
                    request.Branch ?? "main",
                    request.AccessToken,
                    ct);

            if (fileNames.Count == 0)
                return BadRequest(new { error = "Could not read repository contents. The repo may be private or not found — add an access token." });

            var query = new DetectStackQuery(fileNames, pkgJson, reqsTxt, composerJson, gemfile);
            return ToResponse(await Mediator.Send(query, ct));
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new { error = $"Failed to reach repository: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

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

    // ──────────────────────────────────────────────────────────────────────────
    //  GitHub / GitLab repo file fetcher
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<(List<string> FileNames,
        string? PackageJson, string? RequirementsTxt,
        string? ComposerJson, string? Gemfile)>
        FetchRepoContentsAsync(
            IHttpClientFactory factory, string repoUrl, string branch,
            string? accessToken, CancellationToken ct)
    {
        using var http = factory.CreateClient();
        http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("DeployFlow", "1.0"));

        if (!string.IsNullOrEmpty(accessToken))
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

        var ghMatch = System.Text.RegularExpressions.Regex
            .Match(repoUrl, @"github\.com[:/](?<owner>[^/]+)/(?<repo>[^/?.#]+?)(?:\.git)?$");

        if (ghMatch.Success)
        {
            var owner = ghMatch.Groups["owner"].Value;
            var repo  = ghMatch.Groups["repo"].Value;
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/git/trees/{branch}";

            using var resp = await http.GetAsync(apiUrl, ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Try default branch fallback
                apiUrl = $"https://api.github.com/repos/{owner}/{repo}/git/trees/main";
                resp.Dispose();
                using var fallback = await http.GetAsync(apiUrl, ct);
                if (!fallback.IsSuccessStatusCode)
                    return (new(), null, null, null, null);
                return await ParseGitHubFilesAsync(http, owner, repo, branch, fallback, ct);
            }
            return await ParseGitHubFilesAsync(http, owner, repo, branch, resp, ct);
        }

        var glMatch = System.Text.RegularExpressions.Regex
            .Match(repoUrl, @"gitlab\.com/(?<path>[^?.#]+?)(?:\.git)?$");

        if (glMatch.Success)
        {
            var encoded = Uri.EscapeDataString(glMatch.Groups["path"].Value);
            var apiUrl  = $"https://gitlab.com/api/v4/projects/{encoded}/repository/tree?ref={branch}&per_page=100";
            using var resp = await http.GetAsync(apiUrl, ct);
            if (!resp.IsSuccessStatusCode)
                return (new(), null, null, null, null);

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var names = doc.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("name").GetString() ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            async Task<string?> FetchGlFile(string name)
            {
                if (!names.Contains(name)) return null;
                var fileUrl = $"https://gitlab.com/api/v4/projects/{encoded}/repository/files/{Uri.EscapeDataString(name)}/raw?ref={branch}";
                using var r = await http.GetAsync(fileUrl, ct);
                return r.IsSuccessStatusCode ? await r.Content.ReadAsStringAsync(ct) : null;
            }

            return (names,
                await FetchGlFile("package.json"),
                await FetchGlFile("requirements.txt"),
                await FetchGlFile("composer.json"),
                await FetchGlFile("Gemfile"));
        }

        return (new(), null, null, null, null);
    }

    private static async Task<(List<string>, string?, string?, string?, string?)>
        ParseGitHubFilesAsync(HttpClient http, string owner, string repo, string branch,
            HttpResponseMessage resp, CancellationToken ct)
    {
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        List<string> names;
        if (doc.RootElement.TryGetProperty("tree", out var tree))
        {
            names = tree.EnumerateArray()
                .Where(e => e.TryGetProperty("type", out var t) && t.GetString() == "blob")
                .Select(e => e.TryGetProperty("path", out var p) ? System.IO.Path.GetFileName(p.GetString() ?? "") : "")
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();
        }
        else return (new(), null, null, null, null);

        async Task<string?> FetchFile(string name)
        {
            if (!names.Contains(name)) return null;
            var url = $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{name}";
            using var r = await http.GetAsync(url, ct);
            return r.IsSuccessStatusCode ? await r.Content.ReadAsStringAsync(ct) : null;
        }

        return (names,
            await FetchFile("package.json"),
            await FetchFile("requirements.txt"),
            await FetchFile("composer.json"),
            await FetchFile("Gemfile"));
    }
}

public record DetectStackRequest(
    List<string> FileNames,
    string? PackageJsonContent = null,
    string? RequirementsTxtContent = null,
    string? ComposerJsonContent = null,
    string? GemfileContent = null,
    Guid? ProjectId = null
);

public record DetectFromUrlRequest(
    string RepositoryUrl,
    string? Branch = "main",
    string? AccessToken = null
);

public record ApplyStackRequest(
    DeployFlow.Application.Features.StackDetection.DetectedFramework Framework,
    string? BuildCommandOverride,
    string? StartCommandOverride,
    string? InstallCommandOverride,
    int? PortOverride
);
