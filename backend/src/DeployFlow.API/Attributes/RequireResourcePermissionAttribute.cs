using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DeployFlow.API.Attributes;

/// <summary>
/// Action filter attribute that enforces resource-level RBAC.
///
/// Usage:
///   [RequireResourcePermission(PermissionResource.Project, ResourceAction.Deploy, RouteParam = "id")]
///
/// The attribute resolves the resource ID from the route parameter named <see cref="RouteParam"/> (default: "id"),
/// then checks via <see cref="IPermissionService"/> whether the calling user holds the required action.
/// Admins bypass the check (full Manage). Tenant isolation is enforced separately by each handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequireResourcePermissionAttribute : TypeFilterAttribute
{
    public RequireResourcePermissionAttribute(
        PermissionResource resource,
        ResourceAction action,
        string routeParam = "id")
        : base(typeof(ResourcePermissionFilter))
    {
        Arguments = [resource, action, routeParam];
    }
}

internal sealed class ResourcePermissionFilter : IAsyncActionFilter
{
    private readonly IPermissionService _permissions;
    private readonly ICurrentUser _currentUser;
    private readonly PermissionResource _resource;
    private readonly ResourceAction _action;
    private readonly string _routeParam;

    public ResourcePermissionFilter(
        IPermissionService permissions,
        ICurrentUser currentUser,
        PermissionResource resource,
        ResourceAction action,
        string routeParam)
    {
        _permissions = permissions;
        _currentUser = currentUser;
        _resource = resource;
        _action = action;
        _routeParam = routeParam;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_currentUser.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Admins bypass resource-level checks
        if (_currentUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // Resolve resource ID from route
        if (!context.RouteData.Values.TryGetValue(_routeParam, out var raw) ||
            !Guid.TryParse(raw?.ToString(), out var resourceId))
        {
            // No resource ID in route — defer to handler
            await next();
            return;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserId,
            _currentUser.Role,
            _resource,
            resourceId,
            _action);

        if (!allowed)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
