namespace DeployFlow.Application.DTOs;

public record ProjectDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string Status,
    string? RepositoryUrl,
    string? Branch,
    string BuildCommand,
    string StartCommand,
    string? InstallCommand,
    string? DockerfilePath,
    string Framework,
    string? CustomDomain,
    int? Port,
    bool AutoDeploy,
    string? LastDeploymentId,
    string? LastDeploymentStatus,
    DateTime? LastDeployedAt,
    int TotalDeployments,
    string[] Tags,
    Guid? AssignedServerId,
    string? AssignedServerName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ProjectSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string? RepositoryUrl,
    string? Branch,
    string Framework,
    string? LastDeploymentStatus,
    DateTime? LastDeployedAt,
    int TotalDeployments,
    string[] Tags,
    DateTime CreatedAt
);

public record CreateProjectRequest(
    string Name,
    string Description,
    string? RepositoryUrl,
    string Branch,
    string BuildCommand,
    string StartCommand,
    string? InstallCommand,
    string? DockerfilePath,
    string Framework,
    string? CustomDomain,
    int? Port,
    bool AutoDeploy,
    string[] Tags,
    Guid? AssignedServerId
);

public record UpdateProjectRequest(
    string? Name,
    string? Description,
    string? RepositoryUrl,
    string? Branch,
    string? BuildCommand,
    string? StartCommand,
    string? InstallCommand,
    string? DockerfilePath,
    string? CustomDomain,
    int? Port,
    bool? AutoDeploy,
    string[]? Tags,
    Guid? AssignedServerId
);
