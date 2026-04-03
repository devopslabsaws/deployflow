using DeployFlow.Application.Features.Costs.Commands;
using DeployFlow.Application.Features.Costs.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

/// <summary>Cost analytics endpoints.</summary>
[Route("api/costs")]
[Authorize]
public class CostsController : BaseController
{
    public CostsController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Returns rich cost analytics dashboard data: trend, by-category breakdown,
    /// anomaly detection, forecast, and optimization tips.
    /// </summary>
    /// <param name="period">1m | 3m | 6m | 1y  (default: 3m)</param>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] string period = "3m",
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetCostDashboardQuery(period), ct);
        return ToResponse(result);
    }

    /// <summary>Per-resource breakdown suitable for drill-down tables.</summary>
    [HttpGet("breakdown")]
    public async Task<IActionResult> GetBreakdown(
        [FromQuery] string period = "3m",
        [FromQuery] string groupBy = "resource",
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetCostBreakdownQuery(period, groupBy), ct);
        return ToResponse(result);
    }

    /// <summary>Set or update a monthly budget alert threshold.</summary>
    [HttpPost("budget")]
    public async Task<IActionResult> SetBudget([FromBody] SetBudgetRequest request, CancellationToken ct)
    {
        // Persist budget threshold in cache (no migrations needed)
        var result = await Mediator.Send(new SetBudgetCommand(request.MonthlyBudget, request.AlertAt), ct);
        return ToResponse(result);
    }
}

public record SetBudgetRequest(decimal MonthlyBudget, decimal AlertAt = 80);
