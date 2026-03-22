using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using AutoMapper;

namespace DeployFlow.Application.Features.Dashboard.Queries;

public record GetDashboardStatsQuery : IRequest<Result<DashboardStatsDto>>;

public class GetDashboardStatsQueryHandler : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStatsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetDashboardStatsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<DashboardStatsDto>> Handle(GetDashboardStatsQuery request, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var twoWeeksAgo = today.AddDays(-14);

        // Run all independent DB queries in parallel — reduces latency from ~9 round-trips to ~1
        var totalProjectsTask        = _uow.Projects.CountActiveAsync(tenantId, ct);
        var activeDeploymentsTask    = _uow.Deployments.CountActiveAsync(tenantId, ct);
        var serverCountsTask         = _uow.Servers.GetCountsAsync(tenantId, ct);
        var pendingAlertsTask        = _uow.Alerts.CountActiveAsync(tenantId, ct);
        var deploymentsThisMonthTask = _uow.Deployments.CountByRangeAsync(tenantId, monthStart, DateTime.UtcNow, ct);
        var monthlyCostTask          = _uow.CostRecords.GetMonthlyCostAsync(tenantId, today.Year, today.Month, ct);
        var trendTask                = _uow.Deployments.GetDailyStatsAsync(tenantId, twoWeeksAgo, today.AddDays(1), ct);
        var totalDatabasesTask       = _uow.Databases.CountAsync(db => db.TenantId == tenantId, ct);
        var avgDurationTask          = _uow.Deployments.GetAvgDurationSecondsAsync(tenantId, monthStart, DateTime.UtcNow, ct);

        await Task.WhenAll(
            totalProjectsTask, activeDeploymentsTask, serverCountsTask,
            pendingAlertsTask, deploymentsThisMonthTask, monthlyCostTask,
            trendTask, totalDatabasesTask, avgDurationTask);

        var totalProjects        = totalProjectsTask.Result;
        var activeDeployments    = activeDeploymentsTask.Result;
        var (onlineServers, totalServers) = serverCountsTask.Result;
        var pendingAlerts        = pendingAlertsTask.Result;
        var deploymentsThisMonth = deploymentsThisMonthTask.Result;
        var monthlyCost          = monthlyCostTask.Result;
        var trend                = trendTask.Result;
        var totalDatabases       = totalDatabasesTask.Result;
        var avgDurationSeconds   = avgDurationTask.Result;

        var trendDtos = trend.Select(t => new DailyDeploymentStatDto(
            t.Date.ToString("yyyy-MM-dd"),
            t.Successful,
            t.Failed,
            t.Cancelled
        )).ToList();

        // Derive today's counts from the already-fetched trend (no extra DB call)
        var todayStr = today.ToString("yyyy-MM-dd");
        var todayStat = trendDtos.FirstOrDefault(t => t.Date == todayStr);
        var deploymentsToday = (todayStat?.Successful ?? 0) + (todayStat?.Failed ?? 0) + (todayStat?.Cancelled ?? 0);
        var successfulToday  = todayStat?.Successful ?? 0;
        var failedToday      = todayStat?.Failed ?? 0;

        return Result<DashboardStatsDto>.Success(new DashboardStatsDto(
            totalProjects,
            activeDeployments,
            onlineServers,
            totalServers,
            pendingAlerts,
            deploymentsToday,
            successfulToday,
            failedToday,
            totalDatabases,
            avgDurationSeconds,
            deploymentsThisMonth,
            monthlyCost,
            trendDtos
        ));
    }
}
