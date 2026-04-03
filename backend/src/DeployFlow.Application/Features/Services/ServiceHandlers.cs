using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;

namespace DeployFlow.Application.Features.Services;

internal static class ServiceMappings
{
    public static ServiceDto ToDto(this Service service) => new(
        service.Id,
        service.ProjectId,
        service.Name,
        service.Type.ToString(),
        service.Status.ToString(),
        service.Image,
        service.Tag,
        service.Replicas,
        service.ContainerId,
        service.CpuLimit,
        service.MemoryLimit,
        service.CpuRequest,
        service.MemoryRequest,
        service.MinReplicas,
        service.MaxReplicas,
        service.CpuTargetPercentage,
        service.MemoryTargetPercentage,
        service.LastScalingAction,
        service.LastScalingReason,
        service.LastScaledAt,
        service.CreatedAt,
        service.UpdatedAt);

    public static ServiceScalingPolicyDto ToScalingPolicyDto(this Service service) => new(
        service.Id,
        service.Replicas,
        service.MinReplicas,
        service.MaxReplicas,
        service.CpuTargetPercentage,
        service.MemoryTargetPercentage,
        service.LastScalingAction,
        service.LastScalingReason,
        service.LastScaledAt);
}

// ─── Get Services ─────────────────────────────────────────────────────────────

public record GetServicesQuery(Guid? ProjectId = null) : IRequest<Result<List<ServiceDto>>>;
public record GetServiceQuery(Guid Id) : IRequest<Result<ServiceDto>>;
public record GetServiceScalingPolicyQuery(Guid Id) : IRequest<Result<ServiceScalingPolicyDto>>;
public record UpdateServiceScalingPolicyCommand(
    Guid Id,
    int MinReplicas,
    int MaxReplicas,
    int? CpuTargetPercentage,
    int? MemoryTargetPercentage,
    string? TriggerReason = null,
    string? LastScalingAction = null) : IRequest<Result<ServiceScalingPolicyDto>>;

public class GetServicesQueryHandler : IRequestHandler<GetServicesQuery, Result<List<ServiceDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetServicesQueryHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<List<ServiceDto>>> Handle(GetServicesQuery request, CancellationToken ct)
    {
        var services = request.ProjectId.HasValue
            ? await _uow.Services.GetByProjectAsync(request.ProjectId.Value, ct)
            : await _uow.Services.GetByTenantAsync(_currentUser.TenantId, ct);

        var dtos = services.Select(s => s.ToDto()).ToList();

        return Result<List<ServiceDto>>.Success(dtos);
    }
}

public class GetServiceQueryHandler : IRequestHandler<GetServiceQuery, Result<ServiceDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetServiceQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ServiceDto>> Handle(GetServiceQuery request, CancellationToken ct)
    {
        var service = await _uow.Services.GetByIdAsync(request.Id, ct);
        if (service is null || service.TenantId != _currentUser.TenantId)
            return Result<ServiceDto>.Failure("Service not found.", 404);

        return Result<ServiceDto>.Success(service.ToDto());
    }
}

public class GetServiceScalingPolicyQueryHandler : IRequestHandler<GetServiceScalingPolicyQuery, Result<ServiceScalingPolicyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetServiceScalingPolicyQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ServiceScalingPolicyDto>> Handle(GetServiceScalingPolicyQuery request, CancellationToken ct)
    {
        var service = await _uow.Services.GetByIdAsync(request.Id, ct);
        if (service is null || service.TenantId != _currentUser.TenantId)
            return Result<ServiceScalingPolicyDto>.Failure("Service not found.", 404);

        return Result<ServiceScalingPolicyDto>.Success(service.ToScalingPolicyDto());
    }
}

public class UpdateServiceScalingPolicyCommandValidator : AbstractValidator<UpdateServiceScalingPolicyCommand>
{
    public UpdateServiceScalingPolicyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.MinReplicas).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxReplicas).GreaterThanOrEqualTo(x => x.MinReplicas);
        RuleFor(x => x.CpuTargetPercentage).InclusiveBetween(1, 100).When(x => x.CpuTargetPercentage.HasValue);
        RuleFor(x => x.MemoryTargetPercentage).InclusiveBetween(1, 100).When(x => x.MemoryTargetPercentage.HasValue);
    }
}

public class UpdateServiceScalingPolicyCommandHandler : IRequestHandler<UpdateServiceScalingPolicyCommand, Result<ServiceScalingPolicyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdateServiceScalingPolicyCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ServiceScalingPolicyDto>> Handle(UpdateServiceScalingPolicyCommand request, CancellationToken ct)
    {
        var service = await _uow.Services.GetByIdAsync(request.Id, ct);
        if (service is null || service.TenantId != _currentUser.TenantId)
            return Result<ServiceScalingPolicyDto>.Failure("Service not found.", 404);

        service.ConfigureScalingPolicy(
            request.MinReplicas,
            request.MaxReplicas,
            request.CpuTargetPercentage,
            request.MemoryTargetPercentage);

        if (!string.IsNullOrWhiteSpace(request.TriggerReason) || !string.IsNullOrWhiteSpace(request.LastScalingAction))
        {
            service.RecordScalingDecision(request.LastScalingAction, request.TriggerReason);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<ServiceScalingPolicyDto>.Success(service.ToScalingPolicyDto());
    }
}

// ─── Create Service ───────────────────────────────────────────────────────────

public record CreateServiceCommand(
    Guid ProjectId,
    string Name,
    string Type,
    string? Image = null,
    string? Tag = null
) : IRequest<Result<ServiceDto>>;

public class CreateServiceCommandValidator : AbstractValidator<CreateServiceCommand>
{
    public CreateServiceCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Type).NotEmpty();
    }
}

public class CreateServiceCommandHandler : IRequestHandler<CreateServiceCommand, Result<ServiceDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateServiceCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<ServiceDto>> Handle(CreateServiceCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ServiceType>(request.Type, ignoreCase: true, out var serviceType))
            return Result<ServiceDto>.Failure("Invalid service type.", 400);

        var service = Service.Create(
            _currentUser.TenantId,
            request.ProjectId,
            request.Name,
            serviceType,
            request.Image);

        await _uow.Services.AddAsync(service, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ServiceDto>.Success(service.ToDto());
    }
}

// ─── Start Service ────────────────────────────────────────────────────────────

public record StartServiceCommand(Guid Id) : IRequest<Result<ServiceDto>>;

public class StartServiceCommandHandler : IRequestHandler<StartServiceCommand, Result<ServiceDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public StartServiceCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<ServiceDto>> Handle(StartServiceCommand request, CancellationToken ct)
    {
        var service = await _uow.Services.GetByIdAsync(request.Id, ct);
        if (service is null || service.TenantId != _currentUser.TenantId)
            return Result<ServiceDto>.Failure("Service not found.", 404);

        service.SetStatus(ServiceStatus.Running);
        await _uow.SaveChangesAsync(ct);

        return Result<ServiceDto>.Success(service.ToDto());
    }
}

// ─── Stop Service ─────────────────────────────────────────────────────────────

public record StopServiceCommand(Guid Id) : IRequest<Result<ServiceDto>>;

public class StopServiceCommandHandler : IRequestHandler<StopServiceCommand, Result<ServiceDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public StopServiceCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<ServiceDto>> Handle(StopServiceCommand request, CancellationToken ct)
    {
        var service = await _uow.Services.GetByIdAsync(request.Id, ct);
        if (service is null || service.TenantId != _currentUser.TenantId)
            return Result<ServiceDto>.Failure("Service not found.", 404);

        service.SetStatus(ServiceStatus.Stopped);
        await _uow.SaveChangesAsync(ct);

        return Result<ServiceDto>.Success(service.ToDto());
    }
}

// ─── Restart Service ──────────────────────────────────────────────────────────

public record RestartServiceCommand(Guid Id) : IRequest<Result<ServiceDto>>;

public class RestartServiceCommandHandler : IRequestHandler<RestartServiceCommand, Result<ServiceDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public RestartServiceCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<ServiceDto>> Handle(RestartServiceCommand request, CancellationToken ct)
    {
        var service = await _uow.Services.GetByIdAsync(request.Id, ct);
        if (service is null || service.TenantId != _currentUser.TenantId)
            return Result<ServiceDto>.Failure("Service not found.", 404);

        service.SetStatus(ServiceStatus.Running);
        await _uow.SaveChangesAsync(ct);

        return Result<ServiceDto>.Success(service.ToDto());
    }
}

// ─── Delete Service ───────────────────────────────────────────────────────────

public record DeleteServiceCommand(Guid Id) : IRequest<Result>;

public class DeleteServiceCommandHandler : IRequestHandler<DeleteServiceCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteServiceCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(DeleteServiceCommand request, CancellationToken ct)
    {
        var service = await _uow.Services.GetByIdAsync(request.Id, ct);
        if (service is null || service.TenantId != _currentUser.TenantId)
            return Result.Failure("Service not found.", 404);

        service.SoftDelete();
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
