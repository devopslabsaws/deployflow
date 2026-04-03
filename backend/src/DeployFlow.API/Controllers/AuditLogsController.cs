using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/audit-logs")]
public class AuditLogsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AuditLogsController(
        IMediator mediator,
        ApplicationDbContext db,
        ICurrentUser currentUser)
        : base(mediator)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? resourceType = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(l => l.TenantId == _currentUser.TenantId);

        if (!string.IsNullOrWhiteSpace(resourceType))
            query = query.Where(l => l.ResourceType == resourceType);

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                id = l.Id,
                userId = l.UserId,
                userName = l.UserName,
                action = l.Action,
                resourceType = l.ResourceType,
                resourceId = l.ResourceId,
                resourceName = l.ResourceName,
                ipAddress = l.IpAddress,
                userAgent = l.UserAgent,
                metadataJson = l.MetadataJson,
                createdAt = l.CreatedAt,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new
        {
            r.id,
            r.userId,
            r.userName,
            r.action,
            r.resourceType,
            r.resourceId,
            r.resourceName,
            r.ipAddress,
            r.userAgent,
            metadata = string.IsNullOrEmpty(r.metadataJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(r.metadataJson),
            r.createdAt,
        }).ToList();

        return Ok(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }
}
