using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;

namespace DeployFlow.Application.Common;

public interface IJwtService
{
    AuthTokensDto GenerateTokens(ApplicationUser user, Tenant tenant);
}

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid TenantId { get; }
    string Email { get; }
    string Name { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permission);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplateAsync(string to, string templateName, Dictionary<string, string> variables, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyDeploymentStarted(Guid tenantId, Guid projectId, Guid deploymentId, string projectName, CancellationToken ct = default);
    Task NotifyDeploymentSucceeded(Guid tenantId, Guid projectId, Guid deploymentId, string projectName, string? url = null, CancellationToken ct = default);
    Task NotifyDeploymentFailed(Guid tenantId, Guid projectId, Guid deploymentId, string projectName, string? reason = null, CancellationToken ct = default);
    Task NotifyAlertTriggered(Guid tenantId, string alertName, string severity, CancellationToken ct = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default);
}

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

public interface IAuditService
{
    Task LogAsync(
        Guid userId,
        string userName,
        string action,
        string resourceType,
        Guid resourceId,
        string resourceName,
        Guid tenantId,
        string? ipAddress = null,
        string? userAgent = null,
        object? metadata = null,
        CancellationToken ct = default);
}

public interface IDockerService
{
    Task<bool> PullImageAsync(string serverId, string image, string tag = "latest", CancellationToken ct = default);
    Task<string> RunContainerAsync(string serverId, ContainerConfig config, CancellationToken ct = default);
    Task StartContainerAsync(string serverId, string containerId, CancellationToken ct = default);
    Task RestartContainerAsync(string serverId, string containerId, CancellationToken ct = default);
    Task StopContainerAsync(string serverId, string containerId, CancellationToken ct = default);
    Task RemoveContainerAsync(string serverId, string containerId, CancellationToken ct = default);
    Task<ContainerStats> GetContainerStatsAsync(string serverId, string containerId, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamLogsAsync(string serverId, string containerId, bool follow = true, CancellationToken ct = default);
    Task<bool> PingServerAsync(string serverId, CancellationToken ct = default);
    Task<string> BuildImageAsync(string serverId, string buildContextPath, string dockerfilePath, string imageTag, CancellationToken ct = default);
}

public interface ISshService
{
    Task<bool> TestConnectionAsync(string host, int port, string user, string privateKey, CancellationToken ct = default);
    Task<SshCommandResult> ExecuteCommandAsync(string host, int port, string user, string privateKey, string command, CancellationToken ct = default);
    Task<bool> TransferFileAsync(string host, int port, string user, string privateKey, string localPath, string remotePath, CancellationToken ct = default);
}

public interface IGitService
{
    Task<GitCommitInfo?> GetLatestCommitAsync(string repositoryUrl, string branch, string? accessToken = null, CancellationToken ct = default);
    Task<string> CloneRepositoryAsync(string repositoryUrl, string branch, string targetPath, string? accessToken = null, CancellationToken ct = default);
    Task<bool> ValidateRepositoryAccessAsync(string repositoryUrl, string? accessToken = null, CancellationToken ct = default);
}

public record ContainerConfig(
    string Image,
    string Name,
    Dictionary<int, int> PortBindings,
    Dictionary<string, string> EnvVars,
    Dictionary<string, string> VolumeBindings,
    string? Network = null,
    string? RestartPolicy = "unless-stopped",
    string? MemoryLimit = null,
    string? CpuLimit = null);

public record ContainerStats(
    string ContainerId,
    double CpuUsagePercent,
    long MemoryUsageBytes,
    long MemoryLimitBytes,
    long NetworkRxBytes,
    long NetworkTxBytes);

public record SshCommandResult(int ExitCode, string StdOut, string StdErr, bool Success);

public record GitCommitInfo(string Sha, string Message, string Author, string Email, DateTime Timestamp);

/// <summary>
/// Abstraction for broadcasting deployment log/status events over a real-time transport
/// (implemented in the API layer using SignalR, keeping Infrastructure decoupled from SignalR).
/// </summary>
public interface IDeploymentLogBroadcaster
{
    Task BroadcastLogAsync(Guid deploymentId, string message, string? stream, CancellationToken ct = default);
    Task BroadcastStatusAsync(Guid deploymentId, string status, CancellationToken ct = default);
}

/// <summary>
/// Resource-level permission checks. Falls back to tenant-role if no explicit grant exists.
/// </summary>
public interface IPermissionService
{
    /// <summary>Returns true if the current user may perform <paramref name="action"/> on the resource.</summary>
    Task<bool> HasPermissionAsync(
        Guid userId,
        string userRole,
        DeployFlow.Domain.Entities.PermissionResource resourceType,
        Guid resourceId,
        DeployFlow.Domain.Entities.ResourceAction action,
        CancellationToken ct = default);

    /// <summary>Grants (or replaces) a permission on a specific resource for a user.</summary>
    Task GrantAsync(
        Guid tenantId,
        Guid userId,
        DeployFlow.Domain.Entities.PermissionResource resourceType,
        Guid resourceId,
        DeployFlow.Domain.Entities.ResourceAction actions,
        CancellationToken ct = default);

    /// <summary>Revokes all permissions a user has on a specific resource.</summary>
    Task RevokeAsync(
        Guid userId,
        DeployFlow.Domain.Entities.PermissionResource resourceType,
        Guid resourceId,
        CancellationToken ct = default);
}

public interface IS3DestinationValidationService
{
    Task<S3DestinationTestResult> TestConnectionAsync(
        S3DestinationTestRequest request,
        CancellationToken ct = default);
}

public interface IDatabaseRestoreJobService
{
    Task<DatabaseRestoreJobDto> StartAsync(
        Guid databaseId,
        Guid backupId,
        string targetDatabaseName,
        CancellationToken ct = default);

    Task<DatabaseRestoreJobDto?> GetAsync(
        Guid databaseId,
        Guid jobId,
        CancellationToken ct = default);

    Task<bool> HasActiveRestoreAsync(Guid databaseId, CancellationToken ct = default);
}
