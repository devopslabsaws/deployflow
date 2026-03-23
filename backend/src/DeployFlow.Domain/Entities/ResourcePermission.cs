using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

/// <summary>
/// Resource types that can have permissions assigned.
/// </summary>
public enum PermissionResource { Project, Server, Pipeline, Database, Deployment }

/// <summary>
/// Actions that can be permitted or denied on a resource.
/// </summary>
[Flags]
public enum ResourceAction
{
    None     = 0,
    Read     = 1 << 0,
    Deploy   = 1 << 1,
    Configure= 1 << 2,
    Delete   = 1 << 3,
    Manage   = Read | Deploy | Configure | Delete,
}

/// <summary>
/// Project-scoped role permission grant: user X has action Y on resource Z.
/// Overrides the tenant-level role when present.
/// </summary>
public class ResourcePermission : BaseEntity
{
    public Guid UserId { get; private set; }
    public PermissionResource ResourceType { get; private set; }
    public Guid ResourceId { get; private set; }
    public ResourceAction Actions { get; private set; }
    public Guid TenantId { get; private set; }

    private ResourcePermission() { }

    public static ResourcePermission Grant(
        Guid tenantId,
        Guid userId,
        PermissionResource resourceType,
        Guid resourceId,
        ResourceAction actions)
        => new()
        {
            TenantId = tenantId,
            UserId = userId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Actions = actions,
        };

    /// <summary>Replaces the full action set (additive or restrictive).</summary>
    public void SetActions(ResourceAction actions)
    {
        Actions = actions;
        Touch();
    }
}
