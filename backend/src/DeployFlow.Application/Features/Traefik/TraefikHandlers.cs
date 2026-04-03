using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Traefik;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record TraefikRouterDto(
    Guid Id,
    string Name,
    string Rule,
    string ServiceName,
    string? Entrypoints,
    bool TlsEnabled,
    string? CertResolver,
    int Priority,
    bool IsEnabled,
    Guid? ServerId,
    Guid? DomainId,
    DateTime CreatedAt
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetTraefikRoutersQuery(Guid? ServerId) : IRequest<Result<List<TraefikRouterDto>>>;

public class GetTraefikRoutersQueryHandler : IRequestHandler<GetTraefikRoutersQuery, Result<List<TraefikRouterDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetTraefikRoutersQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<TraefikRouterDto>>> Handle(GetTraefikRoutersQuery request, CancellationToken ct)
    {
        var routers = await _uow.TraefikRouters.GetByTenantAsync(_currentUser.TenantId, ct);
        var filtered = request.ServerId.HasValue
            ? routers.Where(r => r.ServerId == request.ServerId.Value)
            : routers;

        return Result<List<TraefikRouterDto>>.Success(filtered.Select(Map).ToList());
    }

    internal static TraefikRouterDto Map(TraefikRouter r) => new(
        r.Id, r.Name, r.Rule, r.ServiceName, r.Entrypoints,
        r.TlsEnabled, r.CertResolver, r.Priority, r.IsEnabled,
        r.ServerId, r.DomainId, r.CreatedAt);
}

// ── Create ────────────────────────────────────────────────────────────────────

public record CreateTraefikRouterCommand(
    string Name,
    string Rule,
    string ServiceName,
    string? Entrypoints,
    bool TlsEnabled,
    string? CertResolver,
    int Priority,
    Guid? ServerId,
    Guid? DomainId
) : IRequest<Result<TraefikRouterDto>>;

public class CreateTraefikRouterCommandHandler : IRequestHandler<CreateTraefikRouterCommand, Result<TraefikRouterDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateTraefikRouterCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<TraefikRouterDto>> Handle(CreateTraefikRouterCommand request, CancellationToken ct)
    {
        var router = new TraefikRouter
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            Rule = request.Rule,
            ServiceName = request.ServiceName,
            Entrypoints = request.Entrypoints ?? "websecure",
            TlsEnabled = request.TlsEnabled,
            CertResolver = request.CertResolver ?? "letsencrypt",
            Priority = request.Priority,
            ServerId = request.ServerId,
            DomainId = request.DomainId,
            IsEnabled = true
        };

        await _uow.TraefikRouters.AddAsync(router, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<TraefikRouterDto>.Success(GetTraefikRoutersQueryHandler.Map(router));
    }
}

// ── Toggle ────────────────────────────────────────────────────────────────────

public record ToggleTraefikRouterCommand(Guid Id, bool IsEnabled) : IRequest<Result<TraefikRouterDto>>;

public class ToggleTraefikRouterCommandHandler : IRequestHandler<ToggleTraefikRouterCommand, Result<TraefikRouterDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ToggleTraefikRouterCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<TraefikRouterDto>> Handle(ToggleTraefikRouterCommand request, CancellationToken ct)
    {
        var router = await _uow.TraefikRouters.GetByIdAsync(request.Id, ct);
        if (router is null || router.TenantId != _currentUser.TenantId)
            return Result<TraefikRouterDto>.Failure("Router not found.", "404");

        router.IsEnabled = request.IsEnabled;
        await _uow.TraefikRouters.UpdateAsync(router, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<TraefikRouterDto>.Success(GetTraefikRoutersQueryHandler.Map(router));
    }
}

// ── Delete ────────────────────────────────────────────────────────────────────

public record DeleteTraefikRouterCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteTraefikRouterCommandHandler : IRequestHandler<DeleteTraefikRouterCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteTraefikRouterCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DeleteTraefikRouterCommand request, CancellationToken ct)
    {
        var router = await _uow.TraefikRouters.GetByIdAsync(request.Id, ct);
        if (router is null || router.TenantId != _currentUser.TenantId)
            return Result<bool>.Failure("Router not found.", "404");

        await _uow.TraefikRouters.DeleteAsync(router, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
