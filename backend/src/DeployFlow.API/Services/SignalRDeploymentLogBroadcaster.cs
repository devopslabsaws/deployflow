using DeployFlow.API.Hubs;
using DeployFlow.Application.Common;
using Microsoft.AspNetCore.SignalR;

namespace DeployFlow.API.Services;

/// <summary>
/// Broadcasts deployment log lines and status changes over SignalR.
/// Injected as a singleton so background services can resolve it without a scope.
/// </summary>
public sealed class SignalRDeploymentLogBroadcaster : IDeploymentLogBroadcaster
{
    private readonly IHubContext<LogStreamHub> _logHub;
    private readonly IHubContext<DeploymentHub> _deployHub;

    public SignalRDeploymentLogBroadcaster(
        IHubContext<LogStreamHub> logHub,
        IHubContext<DeploymentHub> deployHub)
    {
        _logHub = logHub;
        _deployHub = deployHub;
    }

    public Task BroadcastLogAsync(Guid deploymentId, string message, string? stream, CancellationToken ct = default) =>
        _logHub.Clients
               .Group($"deployment-{deploymentId}")
               .SendAsync("log", new { message, stream, timestamp = DateTime.UtcNow }, ct);

    /// <summary>
    /// Sends statusChanged on the same LogStreamHub that the frontend already subscribes to
    /// via SubscribeToDeployment. The DeploymentHub uses project/tenant groups, not
    /// per-deployment groups, so status broadcasts must go through LogStreamHub.
    /// </summary>
    public Task BroadcastStatusAsync(Guid deploymentId, string status, CancellationToken ct = default) =>
        _logHub.Clients
               .Group($"deployment-{deploymentId}")
               .SendAsync("statusChanged", new { deploymentId, status }, ct);
}
