using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using System.Text.Json;

namespace DeployFlow.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _uow;

    public AuditService(IUnitOfWork uow) => _uow = uow;

    public async Task LogAsync(
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
        CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ResourceName = resourceName,
            TenantId = tenantId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            MetadataJson = metadata is not null
                ? JsonSerializer.Serialize(metadata)
                : null
        };

        await _uow.AuditLogs.AddAsync(log, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
