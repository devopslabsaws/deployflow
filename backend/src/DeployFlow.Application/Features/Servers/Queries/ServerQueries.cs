using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Interfaces;
using MediatR;
using AutoMapper;

namespace DeployFlow.Application.Features.Servers.Queries;

public record GetServersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Provider = null
) : IRequest<Result<PaginatedResponse<ServerDto>>>;

public class GetServersQueryHandler : IRequestHandler<GetServersQuery, Result<PaginatedResponse<ServerDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetServersQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<PaginatedResponse<ServerDto>>> Handle(GetServersQuery request, CancellationToken ct)
    {
        var (servers, total) = await _uow.Servers.GetPagedAsync(
            tenantId: _currentUser.TenantId,
            page: request.Page,
            pageSize: request.PageSize,
            status: request.Status,
            provider: request.Provider,
            ct: ct);

        var dtos = _mapper.Map<List<ServerDto>>(servers);
        return Result<PaginatedResponse<ServerDto>>.Success(
            PaginatedResponse<ServerDto>.Create(dtos, total, request.Page, request.PageSize));
    }
}

public record GetServerQuery(Guid Id) : IRequest<Result<ServerDto>>;

public class GetServerQueryHandler : IRequestHandler<GetServerQuery, Result<ServerDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetServerQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<ServerDto>> Handle(GetServerQuery request, CancellationToken ct)
    {
        var server = await _uow.Servers.GetByIdAsync(request.Id, ct);
        if (server is null || server.TenantId != _currentUser.TenantId)
            return Result<ServerDto>.Failure("Server not found.", 404);

        return Result<ServerDto>.Success(_mapper.Map<ServerDto>(server));
    }
}

public record GetServerMetricsQuery(Guid ServerId, int Hours = 24) : IRequest<Result<List<ServerMetricsDto>>>;

public class GetServerMetricsQueryHandler : IRequestHandler<GetServerMetricsQuery, Result<List<ServerMetricsDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetServerMetricsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<List<ServerMetricsDto>>> Handle(GetServerMetricsQuery request, CancellationToken ct)
    {
        var server = await _uow.Servers.GetByIdAsync(request.ServerId, ct);
        if (server is null || server.TenantId != _currentUser.TenantId)
            return Result<List<ServerMetricsDto>>.Failure("Server not found.", 404);

        var since = DateTime.UtcNow.AddHours(-Math.Clamp(request.Hours, 1, 168));
        var metrics = await _uow.Servers.GetMetricsAsync(request.ServerId, since, ct);
        var dtos = _mapper.Map<List<ServerMetricsDto>>(metrics);
        return Result<List<ServerMetricsDto>>.Success(dtos);
    }
}
