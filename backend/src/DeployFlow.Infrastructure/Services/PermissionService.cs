using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.Infrastructure.Services;

/// <summary>
/// Resource-level RBAC: checks explicit grants first, then falls back to
/// tenant-level role defaults.
/// 
/// Role defaults:
///   admin       → Manage (all actions)
///   developer   → Read | Deploy
///   viewer      → Read
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _db;

    public PermissionService(ApplicationDbContext db) => _db = db;

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string userRole,
        PermissionResource resourceType,
        Guid resourceId,
        ResourceAction action,
        CancellationToken ct = default)
    {
        // 1. Explicit resource-level grant
        var grant = await _db.ResourcePermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.ResourceType == resourceType &&
                p.ResourceId == resourceId &&
                !p.IsDeleted, ct);

        if (grant is not null)
            return grant.Actions.HasFlag(action);

        // 2. Fallback: tenant-level role defaults
        return userRole.ToLowerInvariant() switch
        {
            "admin"     => true,
            "developer" => action is ResourceAction.Read or ResourceAction.Deploy,
            "viewer"    => action == ResourceAction.Read,
            _           => false,
        };
    }

    public async Task GrantAsync(
        Guid tenantId,
        Guid userId,
        PermissionResource resourceType,
        Guid resourceId,
        ResourceAction actions,
        CancellationToken ct = default)
    {
        var existing = await _db.ResourcePermissions
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.ResourceType == resourceType &&
                p.ResourceId == resourceId &&
                !p.IsDeleted, ct);

        if (existing is not null)
        {
            existing.SetActions(actions);
        }
        else
        {
            _db.ResourcePermissions.Add(
                ResourcePermission.Grant(tenantId, userId, resourceType, resourceId, actions));
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(
        Guid userId,
        PermissionResource resourceType,
        Guid resourceId,
        CancellationToken ct = default)
    {
        var grants = await _db.ResourcePermissions
            .Where(p =>
                p.UserId == userId &&
                p.ResourceType == resourceType &&
                p.ResourceId == resourceId &&
                !p.IsDeleted)
            .ToListAsync(ct);

        foreach (var g in grants)
            g.SoftDelete();

        await _db.SaveChangesAsync(ct);
    }
}
