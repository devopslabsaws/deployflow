namespace DeployFlow.Application.DTOs;

public record DatabaseInstanceDto(
    Guid Id,
    string Name,
    string Engine,
    string Version,
    string Status,
    string Host,
    int Port,
    string? DatabaseName,
    string? Username,
    string StorageGb,
    bool AutoBackup,
    string? BackupSchedule,
    DateTime? LastBackupAt,
    string? BackupStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateDatabaseRequest(
    string Name,
    string Engine,
    string Version,
    string? DatabaseName,
    string? Username,
    string? Password,
    int StorageGb,
    bool AutoBackup,
    string? BackupSchedule,
    Guid? ServerId
);

public record PipelineDto(
    Guid Id,
    string Name,
    string Description,
    string Status,
    string Trigger,
    Guid? ProjectId,
    string? ProjectName,
    int TotalRuns,
    int SuccessRuns,
    int FailedRuns,
    DateTime? LastRunAt,
    TimeSpan? LastDuration,
    List<PipelineStageDto> Stages,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PipelineStageDto(
    Guid Id,
    string Name,
    string Status,
    int Order,
    List<PipelineStepDto> Steps
);

public record PipelineStepDto(
    Guid Id,
    string Name,
    string Type,
    string Status,
    Dictionary<string, string> Config,
    TimeSpan? Duration
);

public record AlertDto(
    Guid Id,
    string Name,
    string Type,
    string Severity,
    string Condition,
    string Threshold,
    string Status,
    bool IsAcknowledged,
    Guid? AcknowledgedBy,
    DateTime? AcknowledgedAt,
    DateTime? TriggeredAt,
    DateTime CreatedAt
);

public record DomainDto(
    Guid Id,
    string DomainName,
    bool IsVerified,
    bool SslEnabled,
    string? SslIssuer,
    DateTime? SslExpiresAt,
    string? TxtRecord,
    DateTime CreatedAt
);

public record EnvVariableDto(
    Guid Id,
    string Key,
    string? Value,
    bool IsSecret,
    string Environment,
    DateTime CreatedAt
);

public record UpsertEnvVariableRequest(
    string Key,
    string Value,
    bool IsSecret,
    string Environment,
    Guid? ProjectId
);

public record SshKeyDto(
    Guid Id,
    string Name,
    string Fingerprint,
    string PublicKey,
    DateTime CreatedAt
);

public record CreateSshKeyRequest(
    string Name,
    string PrivateKey,
    string? Passphrase
);

public record AuditLogDto(
    Guid Id,
    string UserName,
    string Action,
    string ResourceType,
    string ResourceName,
    string? IpAddress,
    DateTime CreatedAt
);

public record DashboardStatsDto(
    int TotalProjects,
    int ActiveDeployments,
    int OnlineServers,
    int TotalServers,
    int PendingAlerts,
    int DeploymentsToday,
    int SuccessfulDeploymentsToday,
    int FailedDeploymentsToday,
    int TotalDatabases,
    double AvgDeploymentDurationSeconds,
    int DeploymentsThisMonth,
    decimal MonthlyCost,
    List<DailyDeploymentStatDto> DeploymentTrend
);

public record DailyDeploymentStatDto(
    string Date,
    int Successful,
    int Failed,
    int Cancelled
);

public record CostRecordDto(
    Guid Id,
    string ResourceType,
    string ResourceName,
    decimal Amount,
    string Currency,
    string Period,
    DateTime RecordedAt
);

public record ServiceDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Type,
    string Status,
    string? Image,
    string? Tag,
    int Replicas,
    string? ContainerId,
    string? CpuLimit,
    string? MemoryLimit,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
