using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Interfaces;
using MediatR;
using AutoMapper;

namespace DeployFlow.Application.Features.Projects.Queries;

// ─── Get Projects (paginated) ─────────────────────────────────────────────────

public record GetProjectsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null
) : IRequest<Result<PaginatedResponse<ProjectSummaryDto>>>;

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, Result<PaginatedResponse<ProjectSummaryDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetProjectsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<PaginatedResponse<ProjectSummaryDto>>> Handle(GetProjectsQuery request, CancellationToken ct)
    {
        var (projects, total) = await _uow.Projects.GetPagedAsync(
            tenantId: _currentUser.TenantId,
            page: request.Page,
            pageSize: request.PageSize,
            search: request.Search,
            status: request.Status,
            ct: ct);

        var dtos = _mapper.Map<List<ProjectSummaryDto>>(projects);
        return Result<PaginatedResponse<ProjectSummaryDto>>.Success(
            PaginatedResponse<ProjectSummaryDto>.Create(dtos, total, request.Page, request.PageSize));
    }
}

// ─── Get Single Project ───────────────────────────────────────────────────────

public record GetProjectQuery(Guid Id) : IRequest<Result<ProjectDto>>;

public class GetProjectQueryHandler : IRequestHandler<GetProjectQuery, Result<ProjectDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetProjectQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<ProjectDto>> Handle(GetProjectQuery request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.Id, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<ProjectDto>.Failure("Project not found.", 404);

        return Result<ProjectDto>.Success(_mapper.Map<ProjectDto>(project));
    }
}
