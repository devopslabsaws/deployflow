namespace DeployFlow.Application.DTOs;

public record ServerDto(
    Guid Id,
    string Name,
    string IpAddress,
    int SshPort,
    string SshUser,
    string Provider,
    string Status,
    string? Region,
    string? Os,
    string? DockerVersion,
    bool IsSwarmManager,
    bool IsKubernetesEnabled,
    int CpuCores,
    int MemoryGb,
    int DiskGb,
    double CpuUsagePercent,
    double MemoryUsagePercent,
    double DiskUsagePercent,
    int ActiveContainers,
    DateTime? LastHealthCheckAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ServerSummaryDto(
    Guid Id,
    string Name,
    string IpAddress,
    string Provider,
    string Status,
    double CpuUsagePercent,
    double MemoryUsagePercent,
    double DiskUsagePercent,
    int ActiveContainers,
    DateTime? LastHealthCheckAt
);

public record AddServerRequest(
    string Name,
    string IpAddress,
    int SshPort,
    string? SshUser,
    Guid? SshKeyId,
    string Provider,
    string? Region,
    int CpuCores,
    int MemoryGb,
    int DiskGb
);

public record UpdateServerRequest(
    string? Name,
    Guid? SshKeyId,
    int? SshPort,
    string? SshUser,
    string? Region
);

public record ExecResultDto(
    string StdOut,
    string StdErr,
    int ExitCode,
    bool Success
);

public record ServerMetricsDto(
    Guid ServerId,
    DateTime Timestamp,
    double CpuUsagePercent,
    long MemoryUsageBytes,
    long MemoryTotalBytes,
    long DiskUsageBytes,
    long DiskTotalBytes,
    long NetworkRxBytes,
    long NetworkTxBytes,
    int ActiveContainers
);
