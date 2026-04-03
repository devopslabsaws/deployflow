using AutoMapper;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;

namespace DeployFlow.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ── Project ───────────────────────────────────────────────────────────
        CreateMap<Project, ProjectDto>()
            .ConstructUsing(s => new ProjectDto(
                s.Id, s.Name, s.Slug, s.Description ?? "",
                s.Status.ToString(), s.RepositoryUrl, s.RepositoryBranch,
                s.BuildCommand ?? "", s.StartCommand ?? "", s.InstallCommand,
                s.DockerfilePath, s.Framework ?? "", s.CustomDomain, s.Port,
                s.AutoDeployEnabled,
                s.LastDeploymentId.HasValue ? s.LastDeploymentId.ToString() : null,
                s.LastDeploymentStatus != null ? s.LastDeploymentStatus.ToString() : null,
                s.LastDeployedAt, s.DeploymentCount, s.Tags != null ? s.Tags.ToArray() : Array.Empty<string>(),
                s.ServerId, null, s.CreatedAt, s.UpdatedAt))
            .ForAllMembers(o => o.Ignore());

        CreateMap<Project, ProjectSummaryDto>()
            .ConstructUsing(s => new ProjectSummaryDto(
                s.Id, s.Name, s.Slug, s.Status.ToString(),
                s.RepositoryUrl, s.RepositoryBranch, s.Framework ?? "",
                s.LastDeploymentStatus != null ? s.LastDeploymentStatus.ToString() : null,
                s.LastDeployedAt, s.DeploymentCount, s.Tags != null ? s.Tags.ToArray() : Array.Empty<string>(), s.CreatedAt))
            .ForAllMembers(o => o.Ignore());

        // ── Deployment ────────────────────────────────────────────────────────
        CreateMap<Deployment, DeploymentDto>()
            .ConstructUsing(s => new DeploymentDto(
                s.Id,
                s.ProjectId,
                s.Project != null ? s.Project.Name : "",
                s.Status.ToString(),
                s.CommitSha,
                s.CommitMessage,
                s.CommitAuthor,
                s.Branch ?? "main",
                s.Version,
                s.ImageTag,
                s.Url,
                s.Trigger.ToString(),
                s.StartedAt,
                s.FinishedAt,
                s.StartedAt.HasValue && s.FinishedAt.HasValue
                    ? s.FinishedAt.Value - s.StartedAt.Value : (TimeSpan?)null,
                s.ErrorMessage,
                s.PreviousDeploymentId.HasValue ? s.PreviousDeploymentId.ToString() : null,
                s.CreatedAt,
                s.ApprovalStatus.ToString(),
                s.ApprovedBy,
                s.ApprovedAt,
                s.ApprovalNotes,
                s.CanaryStatus.ToString(),
                s.CanaryTrafficPercent,
                s.CanaryStepDurationMinutes,
                s.CanaryStartedAt))
            .ForAllMembers(o => o.Ignore());

        CreateMap<Deployment, DeploymentSummaryDto>()
            .ConstructUsing(s => new DeploymentSummaryDto(
                s.Id,
                s.ProjectId,
                s.Project != null ? s.Project.Name : "",
                s.Status.ToString(),
                s.CommitSha,
                s.CommitMessage,
                s.Branch ?? "main",
                s.Version,
                s.Trigger.ToString(),
                s.StartedAt,
                s.FinishedAt,
                s.ErrorMessage,
                s.CreatedAt))
            .ForAllMembers(o => o.Ignore());

        CreateMap<DeploymentLog, DeploymentLogEntryDto>()
            .ConstructUsing(s => new DeploymentLogEntryDto(
                s.Id,
                s.Level.ToString(),
                s.Message,
                s.Stream,
                s.Timestamp))
            .ForAllMembers(o => o.Ignore());

        // ── Server ────────────────────────────────────────────────────────────
        CreateMap<Server, ServerDto>()
            .ConstructUsing(s => new ServerDto(
                s.Id,
                s.Name,
                s.IpAddress,
                s.SshPort,
                s.SshUser,
                s.Provider.ToString(),
                s.Status.ToString(),
                s.Region,
                s.Os,
                s.DockerVersion,
                s.IsSwarmManager,
                s.KubernetesEnabled,
                s.CpuCount,
                s.MemoryGb,
                s.DiskGb,
                s.CpuUsagePercent,
                s.MemoryUsagePercent,
                s.DiskUsagePercent,
                s.ActiveContainers,
                s.LastHealthCheckAt,
                s.CreatedAt,
                s.UpdatedAt))
            .ForAllMembers(o => o.Ignore());

        CreateMap<Server, ServerSummaryDto>()
            .ConstructUsing(s => new ServerSummaryDto(
                s.Id,
                s.Name,
                s.IpAddress,
                s.Provider.ToString(),
                s.Status.ToString(),
                s.CpuUsagePercent,
                s.MemoryUsagePercent,
                s.DiskUsagePercent,
                s.ActiveContainers,
                s.LastHealthCheckAt))
            .ForAllMembers(o => o.Ignore());

        // ── Pipeline ──────────────────────────────────────────────────────────
        CreateMap<Pipeline, PipelineDto>()
            .ConstructUsing(s => new PipelineDto(
                s.Id,
                s.Name,
                s.Description ?? "",
                s.Status.ToString(),
                s.IsEnabled,
                s.Trigger.ToString(),
                s.ProjectId,
                s.Project != null ? s.Project.Name : null,
                s.TotalRuns,
                s.SuccessRuns,
                s.FailedRuns,
                s.LastRunAt,
                s.LastDuration,
                new List<PipelineStageDto>(), // populated by handler after map
                s.CreatedAt,
                s.UpdatedAt))
            .ForAllMembers(o => o.Ignore());

        CreateMap<PipelineStage, PipelineStageDto>()
            .ConstructUsing(s => new PipelineStageDto(
                s.Id,
                s.Name,
                s.Status.ToString(),
                s.Order,
                new List<PipelineStepDto>()))
            .ForAllMembers(o => o.Ignore());

        CreateMap<PipelineStep, PipelineStepDto>()
            .ConstructUsing(s => new PipelineStepDto(
                s.Id,
                s.Name,
                s.Type.ToString(),
                s.Status.ToString(),
                System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                    s.ConfigJson, (System.Text.Json.JsonSerializerOptions?)null) ?? new(),
                s.Duration))
            .ForAllMembers(o => o.Ignore());

        // ── Alert ─────────────────────────────────────────────────────────────
        CreateMap<Alert, AlertDto>()
            .ConstructUsing(src => new AlertDto(
                src.Id,
                src.Name,
                src.Source,
                src.Severity.ToString(),
                src.Condition ?? "",
                src.Threshold ?? "",
                src.Status.ToString(),
                src.IsAcknowledged,
                src.AcknowledgedById,
                src.AcknowledgedAt,
                src.TriggeredAt,
                src.CreatedAt))
            .ForAllMembers(o => o.Ignore());

        // ── Domain ────────────────────────────────────────────────────────────
        CreateMap<DeployFlow.Domain.Entities.Domain, DomainDto>()
            .ConstructUsing(s => new DomainDto(
                s.Id,
                s.Name,
                s.DnsVerified,
                s.SslEnabled,
                null,           // SslIssuer — not persisted on entity
                s.SslExpiresAt,
                null,           // TxtRecord — set by handler
                s.CreatedAt))
            .ForAllMembers(o => o.Ignore());

        // ── EnvVariable ───────────────────────────────────────────────────────
        CreateMap<EnvVariable, EnvVariableDto>()
            .ConstructUsing(s => new EnvVariableDto(
                s.Id,
                s.Key,
                s.Value,
                s.Type == EnvVarType.Secret,
                "",             // Environment — set contextually by handler
                s.CreatedAt))
            .ForAllMembers(o => o.Ignore());

        // ── DatabaseInstance ──────────────────────────────────────────────────
        CreateMap<DatabaseInstance, DatabaseInstanceDto>()
            .ConstructUsing(s => new DatabaseInstanceDto(
                s.Id,
                s.Name,
                s.Engine.ToString(),
                s.Version,
                s.Status.ToString(),
                s.Host ?? "",
                s.Port ?? 5432,
                s.DatabaseName,
                s.Username,
                s.StorageGb.ToString(),
                s.BackupEnabled,
                s.BackupSchedule,
                s.LastBackupAt,
                null,           // BackupStatus — set by handler
                s.CreatedAt,
                s.UpdatedAt))
            .ForAllMembers(o => o.Ignore());

        // ── S3Destination ────────────────────────────────────────────────────
        CreateMap<S3Destination, S3DestinationDto>()
            .ConstructUsing(s => new S3DestinationDto(
                s.Id,
                s.Name,
                s.Description,
                s.Endpoint,
                s.BucketName,
                s.Region,
                s.IsDefault,
                s.Status.ToString(),
                s.LastTestedAt,
                s.CreatedAt,
                s.UpdatedAt))
            .ForAllMembers(o => o.Ignore());

        // ── BackupPolicy ──────────────────────────────────────────────────────
        CreateMap<BackupPolicy, BackupPolicyDto>()
            .ConstructUsing(s => new BackupPolicyDto(
                s.Id,
                s.DatabaseInstanceId,
                s.IsEnabled,
                s.CronExpression,
                s.RetentionDays,
                s.S3DestinationId,
                s.StorageLocation,
                s.LastRunAt,
                s.NextRunAt,
                s.ErrorMessage,
                s.CreatedAt,
                s.UpdatedAt))
            .ForAllMembers(o => o.Ignore());

        // ── RestoreJob ────────────────────────────────────────────────────────
        CreateMap<RestoreJob, RestoreJobDto>()
            .ConstructUsing(s => new RestoreJobDto(
                s.Id,
                s.DatabaseInstanceId,
                s.BackupId,
                s.TargetDatabaseName,
                s.Status.ToString(),
                s.StartedAt,
                s.CompletedAt,
                s.ErrorMessage,
                s.ProgressPercent,
                s.CreatedAt,
                s.UpdatedAt))
            .ForAllMembers(o => o.Ignore());
    }
}
