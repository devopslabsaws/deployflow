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
}
