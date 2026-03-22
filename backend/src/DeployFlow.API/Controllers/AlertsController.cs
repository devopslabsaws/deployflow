using DeployFlow.Application.Features.Alerts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/alerts")]
public class AlertsController : BaseController
{
    public AlertsController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? severity = null,
        [FromQuery] bool? acknowledged = null,
        CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetAlertsQuery(page, pageSize, severity, acknowledged), ct));

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new AcknowledgeAlertCommand(id), ct));

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new ResolveAlertCommand(id), ct));
}
