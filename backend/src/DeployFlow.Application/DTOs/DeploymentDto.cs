namespace DeployFlow.Application.DTOs;

public record DeploymentDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Status,
    string? CommitSha,
    string? CommitMessage,
    string? CommitAuthor,
    string Branch,
    string? Version,
    string? ImageTag,
    string? Url,
    string Trigger,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    TimeSpan? Duration,
    string? ErrorMessage,
    string? RollbackFromId,
    DateTime CreatedAt,
    // Approval
    string ApprovalStatus,
    Guid? ApprovedBy,
    DateTime? ApprovedAt,
    string? ApprovalNotes,
    // Canary
    string CanaryStatus,
    int CanaryTrafficPercent,
    int CanaryStepDurationMinutes,
    DateTime? CanaryStartedAt
);

public record DeploymentSummaryDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Status,
    string? CommitSha,
    string? CommitMessage,
    string Branch,
    string? Version,
    string Trigger,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? ErrorMessage,
    DateTime CreatedAt
);

public record DeploymentLogEntryDto(
    Guid Id,
    string Level,
    string Message,
    string? Stream,
    DateTime Timestamp
);

public record TriggerDeploymentRequest(
    Guid ProjectId,
    string? Branch,
    string? CommitSha,
    string Trigger = "manual"
);
