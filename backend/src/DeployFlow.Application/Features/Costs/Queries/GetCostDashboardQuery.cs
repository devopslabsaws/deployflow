using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Costs.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetCostDashboardQuery(string Period = "3m") : IRequest<Result<CostDashboardDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetCostDashboardQueryHandler
    : IRequestHandler<GetCostDashboardQuery, Result<CostDashboardDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetCostDashboardQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<CostDashboardDto>> Handle(
        GetCostDashboardQuery request, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        // ── Date range ────────────────────────────────────────────────────
        var (from, to) = ParsePeriod(request.Period);

        var allRecords = await _uow.CostRecords.GetByDateRangeAsync(tenantId, from, to, ct);

        // ── Summary metrics ───────────────────────────────────────────────
        var totalCost = allRecords.Sum(r => r.Amount);

        var now        = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var currentMonthCost = allRecords
            .Where(r => r.RecordedAt >= monthStart)
            .Sum(r => r.Amount);

        // Previous month for change %
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevMonthCost  = allRecords
            .Where(r => r.RecordedAt >= prevMonthStart && r.RecordedAt < monthStart)
            .Sum(r => r.Amount);
        var changePercent  = prevMonthCost > 0
            ? Math.Round((double)((currentMonthCost - prevMonthCost) / prevMonthCost * 100m), 1)
            : 0.0;

        // ── Trend (daily) ─────────────────────────────────────────────────
        var dailyTotals = allRecords
            .GroupBy(r => r.RecordedAt.Date)
            .Select(g => (Date: g.Key, Amount: g.Sum(r => r.Amount)))
            .OrderBy(x => x.Date)
            .ToList();

        // Forecast for remaining days of current month using last-7-day average
        var forecastMonthCost = ForecastCurrentMonth(dailyTotals, monthStart, now);

        var daysRemaining = DateTime.DaysInMonth(now.Year, now.Month) - now.Day;
        decimal dailyForecastRate = dailyTotals.Count > 0
            ? dailyTotals.TakeLast(7).Average(x => x.Amount)
            : 0;

        var trend = dailyTotals
            .Select(x => new CostTrendPoint(x.Date.ToString("yyyy-MM-dd"), x.Amount))
            .ToList();

        for (int i = 1; i <= Math.Min(daysRemaining, 14); i++)
        {
            trend.Add(new CostTrendPoint(
                now.Date.AddDays(i).ToString("yyyy-MM-dd"),
                Amount:   0m,
                Forecast: Math.Round(dailyForecastRate, 2)));
        }

        // ── By category ───────────────────────────────────────────────────
        var byCat = allRecords
            .GroupBy(r => r.ResourceType)
            .Select(g => new { Category = g.Key, Amount = g.Sum(r => r.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var catTotal   = byCat.Sum(x => x.Amount);
        var byCategory = byCat.Select(x => new CostByCategory(
            x.Category,
            Math.Round(x.Amount, 2),
            catTotal > 0 ? Math.Round((double)(x.Amount / catTotal * 100m), 1) : 0.0
        )).ToList();

        // ── Anomaly detection (Z-score on 14-day rolling window) ──────────
        var anomalies = DetectAnomalies(dailyTotals);

        // ── Optimization tips ─────────────────────────────────────────────
        var tips = BuildOptimizationTips(allRecords, dailyForecastRate);

        return Result<CostDashboardDto>.Success(new CostDashboardDto(
            TotalPeriodCost:   Math.Round(totalCost, 2),
            LineItemCount:     allRecords.Count,
            CurrentMonthCost:  Math.Round(currentMonthCost, 2),
            ForecastMonthCost: Math.Round(forecastMonthCost, 2),
            ChangePercent:     (decimal)changePercent,
            Trend:             trend,
            ByCategory:        byCategory,
            Anomalies:         anomalies,
            OptimizationTips:  tips
        ));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static (DateTime From, DateTime To) ParsePeriod(string period)
    {
        var to = DateTime.UtcNow;
        var from = period switch
        {
            "1m" => to.AddMonths(-1),
            "3m" => to.AddMonths(-3),
            "6m" => to.AddMonths(-6),
            "1y" => to.AddYears(-1),
            _    => to.AddMonths(-3),
        };
        return (from, to);
    }

    private static decimal ForecastCurrentMonth(
        List<(DateTime Date, decimal Amount)> dailyTotals,
        DateTime monthStart, DateTime now)
    {
        var thisMonthActual = dailyTotals
            .Where(x => x.Date >= monthStart)
            .Sum(x => x.Amount);

        int daysElapsed   = Math.Max(now.Day, 1);
        int daysInMonth   = DateTime.DaysInMonth(now.Year, now.Month);
        decimal dailyRate = thisMonthActual / daysElapsed;

        return dailyRate * daysInMonth;
    }

    private static List<CostAnomaly> DetectAnomalies(
        List<(DateTime Date, decimal Amount)> dailyTotals)
    {
        var anomalies = new List<CostAnomaly>();
        if (dailyTotals.Count < 7) return anomalies;

        const int windowSize = 14;
        for (int i = windowSize; i < dailyTotals.Count; i++)
        {
            var window = dailyTotals
                .Skip(i - windowSize)
                .Take(windowSize)
                .Select(x => (double)x.Amount)
                .ToList();

            var mean   = window.Average();
            var stdDev = Math.Sqrt(window.Select(v => Math.Pow(v - mean, 2)).Average());

            if (stdDev < 0.01) continue;

            var current = (double)dailyTotals[i].Amount;
            var zScore  = (current - mean) / stdDev;

            if (Math.Abs(zScore) > 2.0)
            {
                anomalies.Add(new CostAnomaly(
                    dailyTotals[i].Date.ToString("yyyy-MM-dd"),
                    "daily-total",
                    Math.Round(dailyTotals[i].Amount, 2),
                    Math.Round((decimal)mean, 2),
                    Math.Round(zScore, 2)
                ));
            }
        }

        return anomalies.OrderByDescending(a => Math.Abs(a.ZScore)).Take(10).ToList();
    }

    private static List<CostOptimizationTip> BuildOptimizationTips(
        IReadOnlyList<Domain.Entities.CostRecord> records, decimal dailyRate)
    {
        var tips = new List<CostOptimizationTip>();

        var computeCost = records
            .Where(r => r.ResourceType == "compute" || r.ResourceType == "server")
            .Sum(r => r.Amount);
        if (computeCost > 0)
            tips.Add(new CostOptimizationTip(
                "Right-size compute instances",
                "Servers with consistently low CPU utilisation (<20%) are candidates for downsizing.",
                Math.Round(computeCost * 0.15m, 2)));

        var storageCost = records.Where(r => r.ResourceType == "storage").Sum(r => r.Amount);
        if (storageCost > 50)
            tips.Add(new CostOptimizationTip(
                "Enable storage lifecycle policies",
                "Move rarely accessed data to cold storage tiers to cut storage spend.",
                Math.Round(storageCost * 0.30m, 2)));

        var dbCost = records.Where(r => r.ResourceType == "database").Sum(r => r.Amount);
        if (dbCost > 30)
            tips.Add(new CostOptimizationTip(
                "Consolidate dev/staging databases",
                "Idle development databases can be paused off-hours to reduce cost.",
                Math.Round(dbCost * 0.40m, 2)));

        return tips.OrderByDescending(t => t.EstimatedSavings).ToList();
    }
}

