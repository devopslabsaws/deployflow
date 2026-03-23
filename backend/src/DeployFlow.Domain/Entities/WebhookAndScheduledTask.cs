#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

/// <summary>
/// Deploy webhook: a secret-signed URL that external services (GitHub, GitLab, etc.)
/// call to trigger a deployment, matching what is shown in the Coolify Webhooks screen.
/// </summary>
public class DeployWebhook : TenantEntity
{
    public Guid ProjectId { get; private set; }

    /// <summary>Stable URL path token — never changes; used to build the inbound URL.</summary>
    public string Token { get; private set; } = Guid.NewGuid().ToString("N");

    /// <summary>Provider-specific secrets used to validate the request signature.</summary>
    public string? GitHubSecret { get; private set; }
    public string? GitLabSecret { get; private set; }
    public string? BitbucketSecret { get; private set; }
    public string? GiteaSecret { get; private set; }

    public bool IsActive { get; private set; } = true;

    private DeployWebhook() { }

    public static DeployWebhook Create(Guid tenantId, Guid projectId) =>
        new() { TenantId = tenantId, ProjectId = projectId };

    public void SetSecrets(
        string? github = null,
        string? gitlab = null,
        string? bitbucket = null,
        string? gitea = null)
    {
        if (github    is not null) GitHubSecret    = github;
        if (gitlab    is not null) GitLabSecret    = gitlab;
        if (bitbucket is not null) BitbucketSecret = bitbucket;
        if (gitea     is not null) GiteaSecret     = gitea;
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }
    public void Activate()   { IsActive = true;  Touch(); }
}

// ─── Scheduled Task ───────────────────────────────────────────────────────────

/// <summary>
/// A cron-driven task that executes a command inside a project's container
/// on a schedule, as shown in the Coolify Scheduled Tasks dialog.
/// </summary>
public class ScheduledTask : TenantEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Command { get; private set; } = default!;

    /// <summary>Standard cron expression, e.g. "0 0 * * *", or the alias "daily".</summary>
    public string Frequency { get; private set; } = "0 0 * * *";

    public int TimeoutSeconds { get; private set; } = 300;

    /// <summary>Optional container name to run the command in (defaults to primary container).</summary>
    public string? ContainerName { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime? LastRunAt { get; private set; }
    public string? LastRunStatus { get; private set; }   // "success" | "failed" | "timeout"
    public string? LastRunOutput { get; private set; }

    private ScheduledTask() { }

    public static ScheduledTask Create(
        Guid tenantId,
        Guid projectId,
        string name,
        string command,
        string frequency = "0 0 * * *",
        int timeoutSeconds = 300,
        string? containerName = null)
        => new()
        {
            TenantId = tenantId,
            ProjectId = projectId,
            Name = name,
            Command = command,
            Frequency = frequency,
            TimeoutSeconds = timeoutSeconds,
            ContainerName = containerName,
        };

    public void Update(
        string? name = null,
        string? command = null,
        string? frequency = null,
        int? timeoutSeconds = null,
        string? containerName = null)
    {
        if (name is not null) Name = name;
        if (command is not null) Command = command;
        if (frequency is not null) Frequency = frequency;
        if (timeoutSeconds.HasValue) TimeoutSeconds = timeoutSeconds.Value;
        if (containerName is not null) ContainerName = containerName;
        Touch();
    }

    public void RecordRun(string status, string? output)
    {
        LastRunAt = DateTime.UtcNow;
        LastRunStatus = status;
        LastRunOutput = output?[..Math.Min(output.Length, 4000)]; // truncate
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }
    public void Activate()   { IsActive = true;  Touch(); }
}
