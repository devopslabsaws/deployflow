using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public PermissionsController(
        IMediator mediator,
        IPermissionService permissions,
        ICurrentUser currentUser)
        : base(mediator)
    {
        _permissions = permissions;
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
