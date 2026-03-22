namespace DeployFlow.Domain.Common;

/// <summary>
/// Base entity with audit fields for all domain objects.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; protected set; } = false;

    public void SoftDelete(Guid? deletedBy = null) => IsDeleted = true;
    public void Touch() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>
/// Base for multi-tenant entities.
/// </summary>
public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}
