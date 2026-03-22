using System.Security.Claims;
using DeployFlow.Application.Common;
using Microsoft.AspNetCore.Http;

namespace DeployFlow.API;

public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public Guid UserId
    {
        get
        {
            var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }
    }

    public Guid TenantId
    {
        get
        {
            var tid = User?.FindFirstValue("tenant_id");
            return Guid.TryParse(tid, out var id) ? id : Guid.Empty;
        }
    }

    public string Email => User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public string Name => User?.FindFirstValue("name") ?? string.Empty;
    public string Role => User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool HasPermission(string permission)
        => Role is "owner" or "admin" ||
           (User?.Claims.Any(c => c.Type == "permission" && c.Value == permission) ?? false);
}
