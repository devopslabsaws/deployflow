using DeployFlow.Infrastructure.BackgroundServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Route("api/runner")]
[ApiController]
[Authorize]
public class RunnerController : ControllerBase
{
    /// <summary>Returns the current health state of the background deployment runner.</summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var started = DeploymentRunnerService.StartedAtUtc;
        var lastPoll = DeploymentRunnerService.LastPollUtc;
        var isRunning = started != DateTime.MinValue
                        && lastPoll != DateTime.MinValue
                        && (DateTime.UtcNow - lastPoll).TotalSeconds < 30;

        return Ok(new
        {
            isRunning,
            startedAtUtc     = started == DateTime.MinValue ? (DateTime?)null : started,
            lastPollUtc      = lastPoll == DateTime.MinValue ? (DateTime?)null : lastPoll,
            secondsSinceLastPoll = lastPoll == DateTime.MinValue
                ? (double?)null
                : (DateTime.UtcNow - lastPoll).TotalSeconds,
            totalPollCycles          = DeploymentRunnerService.TotalPollCycles,
            totalDeploymentsProcessed = DeploymentRunnerService.TotalDeploymentsProcessed,
            activeDeployments        = DeploymentRunnerService.ActiveDeploymentCount,
        });
    }
}
