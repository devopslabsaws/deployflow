using DeployFlow.Domain.Common;
using DeployFlow.Domain.Events;

namespace DeployFlow.Domain.Entities;

public enum ProjectStatus
{
    Active,
    Archived,
    Suspended
}

public class Project : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public ProjectStatus Status { get; private set; } = ProjectStatus.Active;
    public string? RepositoryUrl { get; private set; }
    public string? RepositoryBranch { get; private set; }
    public Guid? ServerId { get; private set; }
    public Guid? EnvironmentId { get; private set; }
    public bool AutoDeployEnabled { get; private set; } = true;
    public bool BranchDeployEnabled { get; private set; } = false;
    public bool PreviewDeployEnabled { get; private set; } = false;
    public string? BuildCommand { get; private set; }
    public string? InstallCommand { get; private set; }
    public string? StartCommand { get; private set; }
    public string? DockerfilePath { get; private set; }
    public string? Framework { get; private set; }
    public string? CustomDomain { get; private set; }
    public string? OutputDirectory { get; private set; }
    public string? RootDirectory { get; private set; }
    public int? Port { get; private set; }
    public string? HealthCheckPath { get; private set; }
    public int HealthCheckTimeout { get; private set; } = 30;
    public DateTime? LastDeployedAt { get; private set; }
    public int DeploymentCount { get; private set; } = 0;
    public Guid? LastDeploymentId { get; private set; }
    public DeploymentStatus? LastDeploymentStatus { get; private set; }
    public List<string> Tags { get; private set; } = new();

    // Navigation
    public ICollection<Deployment> Deployments { get; private set; } = new List<Deployment>();
    public ICollection<Service> Services { get; private set; } = new List<Service>();
    public ICollection<EnvVariable> EnvVariables { get; private set; } = new List<EnvVariable>();

    private Project() { }

    public static Project Create(
        Guid tenantId,
        string name,
        string? description = null,
        string? repositoryUrl = null,
        string? repositoryBranch = "main",
        string? buildCommand = null,
        string? startCommand = null,
        string? installCommand = null,
        string? dockerfilePath = null,
        string? framework = null,
        string? customDomain = null,
        bool autoDeployEnabled = true,
        string[]? tags = null,
        Guid? createdBy = null)
    {
        var slug = System.Text.RegularExpressions.Regex
            .Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
        var project = new Project
        {
            TenantId = tenantId,
            Name = name,
            Slug = slug,
            Description = description,
            RepositoryUrl = repositoryUrl,
            RepositoryBranch = repositoryBranch ?? "main",
            BuildCommand = buildCommand,
            StartCommand = startCommand,
            InstallCommand = installCommand,
            DockerfilePath = dockerfilePath,
            Framework = framework,
            CustomDomain = customDomain,
            AutoDeployEnabled = autoDeployEnabled,
            Tags = tags?.ToList() ?? new List<string>(),
            CreatedBy = createdBy
        };
        project.AddDomainEvent(new ProjectCreatedEvent(project.Id, project.TenantId, name));
        return project;
    }

    public void Update(
        string? name = null,
        string? description = null,
        string? repositoryUrl = null,
        string? repositoryBranch = null,
        bool? autoDeployEnabled = null,
        string? buildCommand = null,
        string? startCommand = null,
        string? installCommand = null,
        string? dockerfilePath = null,
        string? framework = null,
        string? customDomain = null,
        int? port = null,
        Guid? updatedBy = null)
    {
        if (name is not null) { Name = name; Slug = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-'); }
        if (description is not null) Description = description;
        if (repositoryUrl is not null) RepositoryUrl = repositoryUrl;
        if (repositoryBranch is not null) RepositoryBranch = repositoryBranch;
        if (autoDeployEnabled.HasValue) AutoDeployEnabled = autoDeployEnabled.Value;
        if (buildCommand is not null) BuildCommand = buildCommand;
        if (startCommand is not null) StartCommand = startCommand;
        if (installCommand is not null) InstallCommand = installCommand;
        if (dockerfilePath is not null) DockerfilePath = dockerfilePath;
        if (framework is not null) Framework = framework;
        if (customDomain is not null) CustomDomain = customDomain;
        if (port.HasValue) Port = port;
        if (updatedBy.HasValue) UpdatedBy = updatedBy;
        Touch();
    }

    public void Archive()
    {
        Status = ProjectStatus.Archived;
        Touch();
    }

    public void Activate()
    {
        Status = ProjectStatus.Active;
        Touch();
    }

    public void AssignServer(Guid serverId)
    {
        ServerId = serverId;
        Touch();
    }

    public void RecordDeployment(Guid deploymentId, DeploymentStatus status)
    {
        LastDeploymentId = deploymentId;
        LastDeploymentStatus = status;
        LastDeployedAt = DateTime.UtcNow;
        DeploymentCount++;
        Touch();
    }

    public void AddTags(IEnumerable<string> tags)
    {
        foreach (var tag in tags.Where(t => !Tags.Contains(t)))
            Tags.Add(tag);
        Touch();
    }
}
