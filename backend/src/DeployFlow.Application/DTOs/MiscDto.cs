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

// ─── Containers ───────────────────────────────────────────────────────────────

public record ContainerDto(
    string Id,
    string Name,
    string Status,
    string? Image,
    string? ImageTag,
    Dictionary<string, string>? Ports,
    double? CpuPercent,
    long? MemoryBytes,
    long? MemoryLimitBytes,
    DateTime? CreatedAt,
    DateTime? StartedAt
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

public record DatabaseBackupDto(
    Guid Id,
    Guid DatabaseInstanceId,
    string FileName,
    string Status,
    long SizeBytes,
    string? StoragePath,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    bool IsAutomatic,
    DateTime CreatedAt
);

public record ValidateRestoreTargetRequest(string? TargetDatabaseName);

public record RestoreTargetValidationDto(
    bool IsValid,
    string Message,
    string? NormalizedTargetDatabaseName
);

public record StartDatabaseRestoreRequest(
    Guid BackupId,
    string? TargetDatabaseName
);

public record DatabaseRestoreJobDto(
    Guid JobId,
    Guid DatabaseId,
    Guid BackupId,
    string Status,
    int ProgressPercent,
    string Message,
    string TargetDatabaseName,
    DateTime StartedAt,
    DateTime? CompletedAt
);

public record S3DestinationTestRequest(
    string Name,
    string Provider,
    string? Endpoint,
    string Bucket,
    string? Region,
    string AccessKey,
    string SecretKey,
    string? PathPrefix,
    bool UseSsl = true
);

public record S3DestinationTestResult(
    bool Success,
    string Message,
    long LatencyMs,
    string ResolvedEndpoint
);

public record PipelineDto(
    Guid Id,
    string Name,
    string Description,
    string Status,
    bool IsEnabled,
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
    DateTime RecordedAt,
    string Category = "compute"
);

// ── Cost Dashboard ────────────────────────────────────────────────────────────

public record CostDashboardDto(
    decimal TotalPeriodCost,
    int LineItemCount,
    decimal CurrentMonthCost,
    decimal ForecastMonthCost,
    decimal ChangePercent,
    List<CostTrendPoint> Trend,
    List<CostByCategory> ByCategory,
    List<CostAnomaly> Anomalies,
    List<CostOptimizationTip> OptimizationTips
);

public record CostTrendPoint(string Date, decimal Amount, decimal? Forecast = null);

public record CostByCategory(string Category, decimal Amount, double Percent);

public record CostAnomaly(string Date, string ResourceType, decimal Amount,
    decimal ExpectedAmount, double ZScore);

public record CostOptimizationTip(string Title, string Description, decimal EstimatedSavings);

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
    string? CpuRequest,
    string? MemoryRequest,
    int MinReplicas,
    int MaxReplicas,
    int? CpuTargetPercentage,
    int? MemoryTargetPercentage,
    string? LastScalingAction,
    string? LastScalingReason,
    DateTime? LastScaledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ServiceScalingPolicyDto(
    Guid ServiceId,
    int CurrentReplicas,
    int MinReplicas,
    int MaxReplicas,
    int? CpuTargetPercentage,
    int? MemoryTargetPercentage,
    string? LastScalingAction,
    string? LastScalingReason,
    DateTime? LastScaledAt
);

public record S3DestinationDto(
    Guid Id,
    string Name,
    string? Description,
    string Endpoint,
    string BucketName,
    string? Region,
    bool IsDefault,
    string Status,
    DateTime? LastTestedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateS3DestinationRequest(
    string Name,
    string? Description,
    string Endpoint,
    string BucketName,
    string AccessKeyId,
    string SecretAccessKey,
    string? Region,
    bool IsDefault = false
);

public record UpdateS3DestinationRequest(
    string Name,
    string? Description,
    string Endpoint,
    string BucketName,
    string? AccessKeyId,
    string? SecretAccessKey,
    string? Region,
    bool IsDefault = false
);

public record BackupPolicyDto(
    Guid Id,
    Guid DatabaseInstanceId,
    bool IsEnabled,
    string CronExpression,
    int RetentionDays,
    Guid? S3DestinationId,
    string StorageLocation,
    DateTime? LastRunAt,
    DateTime? NextRunAt,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateBackupPolicyRequest(
    Guid DatabaseInstanceId,
    bool IsEnabled,
    string? CronExpression,
    int? RetentionDays,
    Guid? S3DestinationId,
    string? StorageLocation
);

public record UpdateBackupPolicyRequest(
    bool? IsEnabled,
    string? CronExpression,
    int? RetentionDays,
    Guid? S3DestinationId,
    string? StorageLocation
);

public record RestoreJobDto(
    Guid Id,
    Guid DatabaseInstanceId,
    Guid BackupId,
    string TargetDatabaseName,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    float? ProgressPercent,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
