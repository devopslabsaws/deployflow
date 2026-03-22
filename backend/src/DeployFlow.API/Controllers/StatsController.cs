using DeployFlow.Application.Features.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Route("api/stats")]
[Authorize]
public class StatsController : BaseController
{
    public StatsController(IMediator mediator) : base(mediator) { }

    /// <summary>Get dashboard statistics and deployment trend.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetDashboardStatsQuery(), ct);
        return ToResponse(result);
    }
}
