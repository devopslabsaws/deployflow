using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Costs.Queries;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CostBreakdownDto(
    string Period,
    string GroupBy,
    List<CostBreakdownItem> Items,
    decimal TotalCost,
    int ItemCount
);

public record CostBreakdownItem(
    string Label,
    string Category,
    decimal Amount,
    double Percentage,
    decimal DailyAverage,
    string Trend   // "up" | "down" | "flat"
);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetCostBreakdownQuery(string Period = "3m", string GroupBy = "resource")
    : IRequest<Result<CostBreakdownDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetCostBreakdownQueryHandler
    : IRequestHandler<GetCostBreakdownQuery, Result<CostBreakdownDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetCostBreakdownQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<CostBreakdownDto>> Handle(
        GetCostBreakdownQuery request, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var (from, to) = ParsePeriod(request.Period);
        var days       = Math.Max(1, (to - from).Days);

        var allRecords = await _uow.CostRecords.GetByDateRangeAsync(tenantId, from, to, ct);

        // Split timeline in half for trend direction
        var midpoint = from.AddDays(days / 2);
        var firstHalf  = allRecords.Where(r => r.RecordedAt <  midpoint).ToList();
        var secondHalf = allRecords.Where(r => r.RecordedAt >= midpoint).ToList();

        // Group by resource type or resource name
        var grouped = request.GroupBy.ToLower() == "name"
            ? allRecords.GroupBy(r => r.ResourceName ?? r.ResourceType)
            : allRecords.GroupBy(r => r.ResourceType);

        var totalCost = allRecords.Sum(r => r.Amount);

        var items = grouped
            .Select(g =>
            {
                var amount       = g.Sum(r => r.Amount);
                var fh           = firstHalf.Where(r =>
                    (request.GroupBy.ToLower() == "name"
                        ? (r.ResourceName ?? r.ResourceType)
                        : r.ResourceType) == g.Key).Sum(r => r.Amount);
                var sh           = secondHalf.Where(r =>
                    (request.GroupBy.ToLower() == "name"
                        ? (r.ResourceName ?? r.ResourceType)
                        : r.ResourceType) == g.Key).Sum(r => r.Amount);

                var trend = sh > fh * 1.05m ? "up" : sh < fh * 0.95m ? "down" : "flat";

                return new CostBreakdownItem(
                    Label:        g.Key,
                    Category:     g.FirstOrDefault()?.ResourceType ?? g.Key,
                    Amount:       Math.Round(amount, 2),
                    Percentage:   totalCost > 0 ? Math.Round((double)(amount / totalCost * 100m), 1) : 0.0,
                    DailyAverage: Math.Round(amount / days, 2),
                    Trend:        trend
                );
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        return Result<CostBreakdownDto>.Success(new CostBreakdownDto(
            Period:    request.Period,
            GroupBy:   request.GroupBy,
            Items:     items,
            TotalCost: Math.Round(totalCost, 2),
            ItemCount: items.Count
        ));
    }

    private static (DateTime From, DateTime To) ParsePeriod(string period)
    {
        var to   = DateTime.UtcNow;
        var from = period switch
        {
            "1m" => to.AddMonths(-1),
            "6m" => to.AddMonths(-6),
            "1y" => to.AddYears(-1),
            _    => to.AddMonths(-3)
        };
        return (from, to);
    }
}
