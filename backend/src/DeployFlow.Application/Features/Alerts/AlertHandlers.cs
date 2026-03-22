using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;

namespace DeployFlow.Application.Features.Alerts;

// â”€â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record GetAlertsQuery(
    int Page = 1, int PageSize = 20,
    string? Severity = null,
    bool? Acknowledged = null
) : IRequest<Result<PaginatedResponse<AlertDto>>>;

public class GetAlertsQueryHandler : IRequestHandler<GetAlertsQuery, Result<PaginatedResponse<AlertDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetAlertsQueryHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<PaginatedResponse<AlertDto>>> Handle(GetAlertsQuery request, CancellationToken ct)
    {
        var (items, total) = await _uow.Alerts.GetPagedAsync(
            _currentUser.TenantId, request.Page, request.PageSize,
            request.Severity, request.Acknowledged, ct);
        var dtos = items.Select(a => _mapper.Map<AlertDto>(a)).ToList();
        return Result<PaginatedResponse<AlertDto>>.Success(
            PaginatedResponse<AlertDto>.Create(dtos, total, request.Page, request.PageSize));
    }
}

// â”€â”€â”€ Acknowledge Alert â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record AcknowledgeAlertCommand(Guid Id) : IRequest<Result<AlertDto>>;

public class AcknowledgeAlertCommandHandler : IRequestHandler<AcknowledgeAlertCommand, Result<AlertDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public AcknowledgeAlertCommandHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<AlertDto>> Handle(AcknowledgeAlertCommand request, CancellationToken ct)
    {
        var alert = await _uow.Alerts.GetByIdAsync(request.Id, ct);
        if (alert is null || alert.TenantId != _currentUser.TenantId)
            return Result<AlertDto>.Failure("Alert not found.", 404);

        if (alert.IsAcknowledged)
            return Result<AlertDto>.Failure("Alert is already acknowledged.");

        alert.Acknowledge(_currentUser.UserId, _currentUser.Name);
        await _uow.SaveChangesAsync(ct);
        return Result<AlertDto>.Success(_mapper.Map<AlertDto>(alert));
    }
}

// â”€â”€â”€ Resolve Alert â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record ResolveAlertCommand(Guid Id) : IRequest<Result<AlertDto>>;

public class ResolveAlertCommandHandler : IRequestHandler<ResolveAlertCommand, Result<AlertDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public ResolveAlertCommandHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<AlertDto>> Handle(ResolveAlertCommand request, CancellationToken ct)
    {
        var alert = await _uow.Alerts.GetByIdAsync(request.Id, ct);
        if (alert is null || alert.TenantId != _currentUser.TenantId)
            return Result<AlertDto>.Failure("Alert not found.", 404);

        alert.ResolvedAt = DateTime.UtcNow;
        alert.Status = AlertStatus.Resolved;
        await _uow.SaveChangesAsync(ct);
        return Result<AlertDto>.Success(_mapper.Map<AlertDto>(alert));
    }
}
