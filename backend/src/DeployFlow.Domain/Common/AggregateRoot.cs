namespace DeployFlow.Domain.Common;

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent { }

/// <summary>
/// Aggregate root base with domain event support.
/// </summary>
public abstract class AggregateRoot : TenantEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents()
        => _domainEvents.Clear();
}
