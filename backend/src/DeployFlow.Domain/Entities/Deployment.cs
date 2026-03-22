using DeployFlow.Domain.Common;
using DeployFlow.Domain.Events;

namespace DeployFlow.Domain.Entities;

public enum DeploymentStatus
{
    Queued,
    Building,
    Deploying,
    Running,
    Healthy,
    Unhealthy,
    Failed,
    Cancelled,
    Stopped,
    RolledBack
}

public enum DeploymentTrigger
{
    GitPush,
    Manual,
    Api,
    Schedule,
    Rollback
}

public class Deployment : AggregateRoot
{
    public Guid ProjectId { get; private set; }
    public DeploymentStatus Status { get; private set; } = DeploymentStatus.Queued;
    public DeploymentTrigger Trigger { get; private set; }
    public string? CommitSha { get; private set; }
    public string? CommitMessage { get; private set; }
    public string? CommitAuthor { get; private set; }
    public string? Branch { get; private set; }
    public Guid? EnvironmentId { get; private set; }
    public Guid? ServerId { get; private set; }
    public Guid? TriggeredBy { get; private set; }
    public string? ImageTag { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public int? DurationSeconds { get; private set; }
    public string? Url { get; private set; }
    public Guid? PreviousDeploymentId { get; private set; }
    public bool IsRollback { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();

    public bool CanBeCancelled =>
        Status is DeploymentStatus.Queued or DeploymentStatus.Building or DeploymentStatus.Deploying;

    // Navigation
    public Project Project { get; private set; } = default!;
    public ICollection<DeploymentLog> Logs { get; private set; } = new List<DeploymentLog>();

    private Deployment() { }

    public static Deployment Create(
        Guid tenantId,
        Guid projectId,
        DeploymentTrigger trigger = DeploymentTrigger.Manual,
        string? branch = null,
        string? commitSha = null,
        string? commitMessage = null,
        string? commitAuthor = null,
        Guid? environmentId = null,
        Guid? serverId = null,
        Guid? triggeredBy = null)
    {
        var deployment = new Deployment
        {
            TenantId = tenantId,
            ProjectId = projectId,
            Trigger = trigger,
            Branch = branch,
            CommitSha = commitSha,
            CommitMessage = commitMessage,
            CommitAuthor = commitAuthor,
            EnvironmentId = environmentId,
            ServerId = serverId,
            TriggeredBy = triggeredBy,
        };
        deployment.AddDomainEvent(new DeploymentCreatedEvent(deployment.Id, projectId));
        return deployment;
    }

    public static Deployment CreateRollback(Deployment original, Guid triggeredBy)
    {
        var rollback = Create(
            original.TenantId, original.ProjectId,
            DeploymentTrigger.Rollback,
            original.Branch, original.CommitSha,
            $"Rollback of {original.CommitSha?[..7] ?? original.Id.ToString()[..8]}",
            null, original.EnvironmentId, original.ServerId, triggeredBy);
        rollback.IsRollback = true;
        rollback.PreviousDeploymentId = original.Id;
        rollback.ImageTag = original.ImageTag;
        return rollback;
    }

    public void Start()
    {
        Status = DeploymentStatus.Building;
        StartedAt = DateTime.UtcNow;
        Touch();
        AddDomainEvent(new DeploymentStatusChangedEvent(Id, ProjectId, Status));
    }

    public void SetDeploying()
    {
        Status = DeploymentStatus.Deploying;
        Touch();
        AddDomainEvent(new DeploymentStatusChangedEvent(Id, ProjectId, Status));
    }

    public void MarkSucceeded(string? url = null)
    {
        Status = DeploymentStatus.Healthy;
        Url = url;
        Finish();
        AddDomainEvent(new DeploymentSucceededEvent(Id, ProjectId, TenantId));
    }

    public void MarkFailed(string? errorMessage = null)
    {
        Status = DeploymentStatus.Failed;
        ErrorMessage = errorMessage;
        Finish();
        AddDomainEvent(new DeploymentFailedEvent(Id, ProjectId, TenantId, errorMessage));
    }

    public void Cancel()
    {
        if (Status is DeploymentStatus.Queued or DeploymentStatus.Building or DeploymentStatus.Deploying)
        {
            Status = DeploymentStatus.Cancelled;
            Finish();
        }
    }

    public void SetImageTag(string imageTag)
    {
        ImageTag = imageTag;
        Touch();
    }

    private void Finish()
    {
        FinishedAt = DateTime.UtcNow;
        if (StartedAt.HasValue)
            DurationSeconds = (int)(FinishedAt.Value - StartedAt.Value).TotalSeconds;
        Touch();
    }
}
