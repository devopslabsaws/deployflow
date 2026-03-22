using DeployFlow.Domain.Entities;

namespace DeployFlow.Domain.Events;

public record ProjectCreatedEvent(Guid ProjectId, Guid TenantId, string ProjectName) : Common.IDomainEvent;

public record DeploymentCreatedEvent(Guid DeploymentId, Guid ProjectId) : Common.IDomainEvent;

public record DeploymentStatusChangedEvent(
    Guid DeploymentId,
    Guid ProjectId,
    DeploymentStatus Status) : Common.IDomainEvent;

public record DeploymentSucceededEvent(
    Guid DeploymentId,
    Guid ProjectId,
    Guid TenantId) : Common.IDomainEvent;

public record DeploymentFailedEvent(
    Guid DeploymentId,
    Guid ProjectId,
    Guid TenantId,
    string? ErrorMessage) : Common.IDomainEvent;

public record ServerStatusChangedEvent(
    Guid ServerId,
    ServerStatus OldStatus,
    ServerStatus NewStatus) : Common.IDomainEvent;

public record AlertTriggeredEvent(
    Guid AlertId,
    string AlertName,
    AlertSeverity Severity,
    Guid TenantId) : Common.IDomainEvent;
