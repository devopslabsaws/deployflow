using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Interfaces;
using MediatR;
using AutoMapper;

namespace DeployFlow.Application.Features.Deployments.Queries;

public record GetDeploymentsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? ProjectId = null,
    string? Status = null,
    string? Branch = null
) : IRequest<Result<PaginatedResponse<DeploymentSummaryDto>>>;

public class GetDeploymentsQueryHandler : IRequestHandler<GetDeploymentsQuery, Result<PaginatedResponse<DeploymentSummaryDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetDeploymentsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<PaginatedResponse<DeploymentSummaryDto>>> Handle(GetDeploymentsQuery request, CancellationToken ct)
    {
        var (deployments, total) = await _uow.Deployments.GetPagedAsync(
            tenantId: _currentUser.TenantId,
            page: request.Page,
            pageSize: request.PageSize,
            projectId: request.ProjectId,
            status: request.Status,
            branch: request.Branch,
            ct: ct);

        var dtos = _mapper.Map<List<DeploymentSummaryDto>>(deployments);
        return Result<PaginatedResponse<DeploymentSummaryDto>>.Success(
            PaginatedResponse<DeploymentSummaryDto>.Create(dtos, total, request.Page, request.PageSize));
    }
}

public record GetDeploymentQuery(Guid Id) : IRequest<Result<DeploymentDto>>;

public class GetDeploymentQueryHandler : IRequestHandler<GetDeploymentQuery, Result<DeploymentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetDeploymentQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<DeploymentDto>> Handle(GetDeploymentQuery request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.Id, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result<DeploymentDto>.Failure("Deployment not found.", 404);

        return Result<DeploymentDto>.Success(_mapper.Map<DeploymentDto>(deployment));
    }
}

public record GetDeploymentLogsQuery(Guid DeploymentId, int Page = 1, int PageSize = 100) 
    : IRequest<Result<PaginatedResponse<DeploymentLogEntryDto>>>;

public class GetDeploymentLogsQueryHandler : IRequestHandler<GetDeploymentLogsQuery, Result<PaginatedResponse<DeploymentLogEntryDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetDeploymentLogsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IMapper mapper)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<PaginatedResponse<DeploymentLogEntryDto>>> Handle(GetDeploymentLogsQuery request, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetByIdAsync(request.DeploymentId, ct);
        if (deployment is null || deployment.TenantId != _currentUser.TenantId)
            return Result<PaginatedResponse<DeploymentLogEntryDto>>.Failure("Deployment not found.", 404);

        var (logs, total) = await _uow.Deployments.GetLogsPagedAsync(
            request.DeploymentId, request.Page, request.PageSize, ct);

        var dtos = _mapper.Map<List<DeploymentLogEntryDto>>(logs);
        return Result<PaginatedResponse<DeploymentLogEntryDto>>.Success(
            PaginatedResponse<DeploymentLogEntryDto>.Create(dtos, total, request.Page, request.PageSize));
    }
}
