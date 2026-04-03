using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Insights;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record ProjectSloDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    double UptimeTargetPercent,
    int P95LatencyMs,
    double ErrorRateBudgetPercent,
    int WindowDays,
    bool IsEnabled,
    double? CurrentUptimePercent,
    double? ErrorBudgetRemainingPercent,
    DateTime? LastEvaluatedAt,
    // Derived
    bool IsBreaching,
    double? ErrorBudgetBurnRate
);

public record SloEventCorrelation(
    DateTime DeployedAt,
    string ProjectName,
    string Branch,
    string CommitSha,
    string Status,
    double? UptimeBefore,
    double? UptimeAfter
);

public record SloReportDto(
    Guid ProjectId,
    string ProjectName,
    double UptimeTargetPercent,
    int WindowDays,
    double? CurrentUptimePercent,
    double? ErrorBudgetRemainingPercent,
    bool IsBreaching,
    List<SloEventCorrelation> RecentDeployEvents,
    DateTime GeneratedAt
);

// ─── Queries ─────────────────────────────────────────────────────────────────

public record GetProjectSlosQuery(Guid? ProjectId = null) : IRequest<Result<List<ProjectSloDto>>>;

public class GetProjectSlosQueryHandler : IRequestHandler<GetProjectSlosQuery, Result<List<ProjectSloDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetProjectSlosQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<ProjectSloDto>>> Handle(GetProjectSlosQuery request, CancellationToken ct)
    {
        var slos = await _uow.ProjectSlos.GetByTenantAsync(_currentUser.TenantId, ct);
        var projects = await _uow.Projects.GetByTenantAsync(_currentUser.TenantId, ct);
        var projectMap = projects.ToDictionary(p => p.Id, p => p.Name);

        var filtered = request.ProjectId.HasValue
            ? slos.Where(s => s.ProjectId == request.ProjectId.Value)
            : slos;

        return Result<List<ProjectSloDto>>.Success(
            filtered.Select(s => MapSlo(s, projectMap.GetValueOrDefault(s.ProjectId, "Unknown"))).ToList());
    }

    internal static ProjectSloDto MapSlo(ProjectSlo s, string projectName)
    {
        var burnRate = s.UptimeTargetPercent > 0 && s.ErrorBudgetRemainingPercent.HasValue
            ? Math.Round((1.0 - s.ErrorBudgetRemainingPercent.Value / 100.0) * (100.0 / (100.0 - s.UptimeTargetPercent)), 2)
            : (double?)null;

        var isBreaching = s.CurrentUptimePercent.HasValue && s.CurrentUptimePercent.Value < s.UptimeTargetPercent;

        return new ProjectSloDto(
            s.Id, s.ProjectId, projectName,
            s.UptimeTargetPercent, s.P95LatencyMs, s.ErrorRateBudgetPercent,
            s.WindowDays, s.IsEnabled,
            s.CurrentUptimePercent, s.ErrorBudgetRemainingPercent,
            s.LastEvaluatedAt, isBreaching, burnRate);
    }
}

/// <summary>Fetches an SLO report including recent deployment events correlated to uptime changes.</summary>
public record GetSloReportQuery(Guid ProjectId) : IRequest<Result<SloReportDto>>;

public class GetSloReportQueryHandler : IRequestHandler<GetSloReportQuery, Result<SloReportDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetSloReportQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<SloReportDto>> Handle(GetSloReportQuery request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<SloReportDto>.Failure("Project not found.", "404");

        var slos = await _uow.ProjectSlos.GetByTenantAsync(_currentUser.TenantId, ct);
        var slo = slos.FirstOrDefault(s => s.ProjectId == request.ProjectId);

        // Build deploy event correlations from recent deployments
        var (recentDeployments, _) = await _uow.Deployments.GetPagedAsync(
            _currentUser.TenantId, page: 1, pageSize: 20, projectId: request.ProjectId, ct: ct);
        var recent = recentDeployments.ToList();

        var events = recent.Select((d, i) => new SloEventCorrelation(
            d.StartedAt ?? DateTime.UtcNow,
            project.Name,
            d.Branch ?? "main",
            d.CommitSha?.Take(7) is { } chars ? new string(chars.ToArray()) : "unknown",
            d.Status.ToString(),
            null, // uptime before (would need metric store)
            null  // uptime after
        )).ToList();

        bool isBreaching = slo is not null && slo.CurrentUptimePercent.HasValue
            && slo.CurrentUptimePercent.Value < slo.UptimeTargetPercent;

        return Result<SloReportDto>.Success(new SloReportDto(
            request.ProjectId,
            project.Name,
            slo?.UptimeTargetPercent ?? 99.9,
            slo?.WindowDays ?? 30,
            slo?.CurrentUptimePercent,
            slo?.ErrorBudgetRemainingPercent,
            isBreaching,
            events,
            DateTime.UtcNow
        ));
    }
}

// ─── Commands ─────────────────────────────────────────────────────────────────

public record UpsertProjectSloCommand(
    Guid ProjectId,
    double UptimeTargetPercent,
    int P95LatencyMs,
    double ErrorRateBudgetPercent,
    int WindowDays,
    bool IsEnabled
) : IRequest<Result<ProjectSloDto>>;

public class UpsertProjectSloCommandHandler : IRequestHandler<UpsertProjectSloCommand, Result<ProjectSloDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpsertProjectSloCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ProjectSloDto>> Handle(UpsertProjectSloCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<ProjectSloDto>.Failure("Project not found.", "404");

        var existing = (await _uow.ProjectSlos.GetByTenantAsync(_currentUser.TenantId, ct))
            .FirstOrDefault(s => s.ProjectId == request.ProjectId);

        if (existing is null)
        {
            var newSlo = new ProjectSlo
            {
                TenantId = _currentUser.TenantId,
                ProjectId = request.ProjectId,
                UptimeTargetPercent = request.UptimeTargetPercent,
                P95LatencyMs = request.P95LatencyMs,
                ErrorRateBudgetPercent = request.ErrorRateBudgetPercent,
                WindowDays = request.WindowDays,
                IsEnabled = request.IsEnabled,
            };
            await _uow.ProjectSlos.AddAsync(newSlo, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<ProjectSloDto>.Success(GetProjectSlosQueryHandler.MapSlo(newSlo, project.Name));
        }
        else
        {
            existing.UptimeTargetPercent = request.UptimeTargetPercent;
            existing.P95LatencyMs = request.P95LatencyMs;
            existing.ErrorRateBudgetPercent = request.ErrorRateBudgetPercent;
            existing.WindowDays = request.WindowDays;
            existing.IsEnabled = request.IsEnabled;
            await _uow.ProjectSlos.UpdateAsync(existing, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<ProjectSloDto>.Success(GetProjectSlosQueryHandler.MapSlo(existing, project.Name));
        }
    }
}

public record DeleteProjectSloCommand(Guid ProjectId) : IRequest<Result<bool>>;

public class DeleteProjectSloCommandHandler : IRequestHandler<DeleteProjectSloCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteProjectSloCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DeleteProjectSloCommand request, CancellationToken ct)
    {
        var slos = await _uow.ProjectSlos.GetByTenantAsync(_currentUser.TenantId, ct);
        var slo = slos.FirstOrDefault(s => s.ProjectId == request.ProjectId);
        if (slo is null)
            return Result<bool>.Failure("SLO not found.", "404");

        await _uow.ProjectSlos.DeleteAsync(slo, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
