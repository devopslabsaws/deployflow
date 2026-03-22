using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;

namespace DeployFlow.Application.Features.Services;

// ─── Get Services ─────────────────────────────────────────────────────────────

public record GetServicesQuery(Guid? ProjectId = null) : IRequest<Result<List<ServiceDto>>>;

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

        var dtos = services.Select(s => new ServiceDto(
            s.Id, s.ProjectId, s.Name, s.Type.ToString(), s.Status.ToString(),
            s.Image, s.Tag, s.Replicas, s.ContainerId,
            s.CpuLimit, s.MemoryLimit,
            s.CreatedAt, s.UpdatedAt)).ToList();

        return Result<List<ServiceDto>>.Success(dtos);
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

        return Result<ServiceDto>.Success(new ServiceDto(
            service.Id, service.ProjectId, service.Name,
            service.Type.ToString(), service.Status.ToString(),
            service.Image, service.Tag, service.Replicas, service.ContainerId,
            service.CpuLimit, service.MemoryLimit,
            service.CreatedAt, service.UpdatedAt));
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

        return Result<ServiceDto>.Success(new ServiceDto(
            service.Id, service.ProjectId, service.Name,
            service.Type.ToString(), service.Status.ToString(),
            service.Image, service.Tag, service.Replicas, service.ContainerId,
            service.CpuLimit, service.MemoryLimit,
            service.CreatedAt, service.UpdatedAt));
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

        return Result<ServiceDto>.Success(new ServiceDto(
            service.Id, service.ProjectId, service.Name,
            service.Type.ToString(), service.Status.ToString(),
            service.Image, service.Tag, service.Replicas, service.ContainerId,
            service.CpuLimit, service.MemoryLimit,
            service.CreatedAt, service.UpdatedAt));
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

        return Result<ServiceDto>.Success(new ServiceDto(
            service.Id, service.ProjectId, service.Name,
            service.Type.ToString(), service.Status.ToString(),
            service.Image, service.Tag, service.Replicas, service.ContainerId,
            service.CpuLimit, service.MemoryLimit,
            service.CreatedAt, service.UpdatedAt));
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
