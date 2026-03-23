using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeployFlow.API.Controllers;

/// <summary>
/// Manages resource-level permission grants (admin only for grants/revokes;
/// any authenticated user can query their own permissions).
/// </summary>
[Authorize]
[Route("api/permissions")]
public class PermissionsController : BaseController
{
    private readonly IPermissionService _permissions;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;

    public PermissionsController(
        IMediator mediator,
        IPermissionService permissions,
        ApplicationDbContext db,
        ICurrentUser currentUser)
        : base(mediator)
    {
        _permissions = permissions;
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Check whether the calling user has a specific action on a resource.</summary>
    [HttpGet("check")]
    public async Task<IActionResult> Check(
        [FromQuery] PermissionResource resourceType,
        [FromQuery] Guid resourceId,
        [FromQuery] ResourceAction action,
        CancellationToken ct)
    {
        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserId, _currentUser.Role, resourceType, resourceId, action, ct);
        return Ok(new { allowed, resourceType, resourceId, action = action.ToString() });
    }

    [HttpGet("users/{userId:guid}/grants")]
    public async Task<IActionResult> GetUserGrants(Guid userId, CancellationToken ct)
    {
        if (!_currentUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase) && _currentUser.UserId != userId)
            return Forbid();

        var grants = await _db.ResourcePermissions
            .AsNoTracking()
            .Where(p => p.TenantId == _currentUser.TenantId && p.UserId == userId && !p.IsDeleted)
            .OrderBy(p => p.ResourceType)
            .ThenBy(p => p.ResourceId)
            .ToListAsync(ct);

        var result = grants
            .Select(PermissionRequestMappings.ToDto)
            .ToList();

        return Ok(result);
    }

    [HttpGet("users/{userId:guid}/preview")]
    public async Task<IActionResult> Preview(Guid userId, [FromQuery] PermissionResource resourceType, [FromQuery] Guid resourceId, CancellationToken ct)
    {
        if (!_currentUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase) && _currentUser.UserId != userId)
            return Forbid();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == _currentUser.TenantId, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        var actions = new[] { ResourceAction.Read, ResourceAction.Deploy, ResourceAction.Configure, ResourceAction.Delete };
        var allowed = new List<string>();
        foreach (var action in actions)
        {
            if (await _permissions.HasPermissionAsync(user.Id, user.Role, resourceType, resourceId, action, ct))
                allowed.Add(action.ToString().ToLowerInvariant());
        }

        return Ok(new PermissionPreviewDto(user.Id, resourceType.ToString().ToLowerInvariant(), resourceId, allowed));
    }

    /// <summary>Grant (or replace) a permission. Admin only.</summary>
    [HttpPost("grant")]
    public async Task<IActionResult> Grant([FromBody] GrantPermissionRequest req, CancellationToken ct)
    {
        if (!_currentUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        await _permissions.GrantAsync(
            _currentUser.TenantId, req.UserId, req.ResourceType, req.ResourceId, req.Actions, ct);
        return NoContent();
    }

    [HttpPost("grants/bulk")]
    public async Task<IActionResult> GrantBulk([FromBody] BulkGrantPermissionRequest req, CancellationToken ct)
    {
        if (!_currentUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        foreach (var item in req.Grants)
        {
            await _permissions.GrantAsync(
                _currentUser.TenantId,
                req.UserId,
                item.ResourceType,
                item.ResourceId,
                PermissionRequestMappings.ParseActions(item.Actions),
                ct);
        }

        return NoContent();
    }

    /// <summary>Revoke a permission. Admin only.</summary>
    [HttpDelete("revoke")]
    public async Task<IActionResult> Revoke([FromBody] RevokePermissionRequest req, CancellationToken ct)
    {
        if (!_currentUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        await _permissions.RevokeAsync(req.UserId, req.ResourceType, req.ResourceId, ct);
        return NoContent();
    }
}

public record GrantPermissionRequest(
    Guid UserId,
    PermissionResource ResourceType,
    Guid ResourceId,
    ResourceAction Actions);

public record RevokePermissionRequest(
    Guid UserId,
    PermissionResource ResourceType,
    Guid ResourceId);

public record BulkGrantPermissionRequest(Guid UserId, List<BulkGrantPermissionItemRequest> Grants);

public record BulkGrantPermissionItemRequest(
    PermissionResource ResourceType,
    Guid ResourceId,
    List<string> Actions);

public record PermissionGrantDto(
    Guid Id,
    Guid UserId,
    string ResourceType,
    Guid ResourceId,
    List<string> Actions);

public record PermissionPreviewDto(
    Guid UserId,
    string ResourceType,
    Guid ResourceId,
    List<string> EffectiveActions);

internal static class PermissionRequestMappings
{
    public static ResourceAction ParseActions(IEnumerable<string> actions)
    {
        var value = ResourceAction.None;
        foreach (var action in actions)
        {
            if (Enum.TryParse<ResourceAction>(action, true, out var parsed) && parsed != ResourceAction.Manage)
                value |= parsed;
        }
        return value;
    }

    public static PermissionGrantDto ToDto(ResourcePermission permission)
        => new(
            permission.Id,
            permission.UserId,
            permission.ResourceType.ToString().ToLowerInvariant(),
            permission.ResourceId,
            ToActionNames(permission.Actions));

    private static List<string> ToActionNames(ResourceAction actions)
    {
        var ordered = new[] { ResourceAction.Read, ResourceAction.Deploy, ResourceAction.Configure, ResourceAction.Delete };
        return ordered
            .Where(action => actions.HasFlag(action))
            .Select(a => a.ToString().ToLowerInvariant())
            .ToList();
    }
}
