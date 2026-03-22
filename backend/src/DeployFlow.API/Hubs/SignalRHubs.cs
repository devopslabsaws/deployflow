using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DeployFlow.API.Hubs;

[Authorize]
public class LogStreamHub : Hub
{
    /// <summary>Client subscribes to deployment logs by deployment ID.</summary>
    public async Task SubscribeToDeployment(string deploymentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"deployment-{deploymentId}");
    }

    public async Task UnsubscribeFromDeployment(string deploymentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"deployment-{deploymentId}");
    }

    /// <summary>Client subscribes to server/container live logs.</summary>
    public async Task SubscribeToContainer(string serverId, string containerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"container-{serverId}-{containerId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}

[Authorize]
public class DeploymentHub : Hub
{
    /// <summary>Client subscribes to deployment status changes for a project.</summary>
    public async Task SubscribeToProject(string projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }

    public async Task SubscribeToTenant(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
    }
}
