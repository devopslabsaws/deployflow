using DeployFlow.Application.Common;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DeployFlow.Infrastructure.Services;

public class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;

    public GitService(ILogger<GitService> logger) => _logger = logger;

    public async Task<GitCommitInfo?> GetLatestCommitAsync(
        string repositoryUrl,
        string branch,
        string? accessToken = null,
        CancellationToken ct = default)
    {
        try
        {
            // For GitHub repos, use Octokit for efficiency
            if (repositoryUrl.Contains("github.com"))
            {
                var (owner, repo) = ParseGitHubUrl(repositoryUrl);
                if (owner is not null && repo is not null)
                {
                    var client = new GitHubClient(new ProductHeaderValue("DeployFlow"));
                    if (accessToken is not null)
                        client.Credentials = new Octokit.Credentials(accessToken);

                    var commits = await client.Repository.Commit.GetAll(owner, repo,
                        new CommitRequest { Sha = branch });
                    var latest = commits.FirstOrDefault();
                    if (latest is not null)
                        return new GitCommitInfo(
                            latest.Sha,
                            latest.Commit.Message,
                            latest.Commit.Author.Name,
                            latest.Commit.Author.Email,
                            latest.Commit.Author.Date.UtcDateTime);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch latest commit for {Url}/{Branch}", repositoryUrl, branch);
            return null;
        }
    }

    public Task<string> CloneRepositoryAsync(
        string repositoryUrl,
        string branch,
        string targetPath,
        string? accessToken = null,
        CancellationToken ct = default)
    {
        var cloneOptions = new CloneOptions
        {
            BranchName = branch,
            RecurseSubmodules = false,
        };

        if (accessToken is not null)
        {
            cloneOptions.FetchOptions.CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials { Username = accessToken, Password = "" };
        }

        var path = LibGit2Sharp.Repository.Clone(repositoryUrl, targetPath, cloneOptions);
        return Task.FromResult(path);
    }

    public async Task<bool> ValidateRepositoryAccessAsync(
        string repositoryUrl,
        string? accessToken = null,
        CancellationToken ct = default)
    {
        try
        {
            if (repositoryUrl.Contains("github.com"))
            {
                var (owner, repo) = ParseGitHubUrl(repositoryUrl);
                if (owner is not null && repo is not null)
                {
                    var client = new GitHubClient(new ProductHeaderValue("DeployFlow"));
                    if (accessToken is not null)
                        client.Credentials = new Octokit.Credentials(accessToken);
                    await client.Repository.Get(owner, repo);
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static (string? Owner, string? Repo) ParseGitHubUrl(string url)
    {
        // https://github.com/owner/repo or https://github.com/owner/repo.git
        var uri = new Uri(url);
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length >= 2)
            return (parts[0], parts[1].TrimEnd(".git".ToCharArray()));
        return (null, null);
    }
}
