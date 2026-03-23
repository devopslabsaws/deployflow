using DeployFlow.Application.Features.Alerts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/alert-rules")]
public class AlertRulesController : BaseController
{
    public AlertRulesController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetAlertRules(CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetAlertRulesQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> CreateAlertRule([FromBody] UpsertAlertRuleRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new CreateAlertRuleCommand(
            request.Name,
            request.Metric,
            request.Operator,
            request.Threshold,
            request.WindowMinutes,
            request.Severity,
            request.IsEnabled,
            request.CooldownMinutes,
            request.Description), ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAlertRule(Guid id, [FromBody] UpsertAlertRuleRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new UpdateAlertRuleCommand(
            id,
            request.Name,
            request.Metric,
            request.Operator,
            request.Threshold,
            request.WindowMinutes,
            request.Severity,
            request.IsEnabled,
            request.CooldownMinutes,
            request.Description), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAlertRule(Guid id, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new DeleteAlertRuleCommand(id), ct));

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> TestAlertRule(Guid id, [FromBody] TestAlertRuleRequest? request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new TestAlertRuleCommand(id, request?.SampleValue), ct));
}

public record UpsertAlertRuleRequest(
    string Name,
    string Metric,
    string Operator,
    decimal Threshold,
    int WindowMinutes,
    string Severity,
    bool IsEnabled,
    int CooldownMinutes,
    string? Description
);

public record TestAlertRuleRequest(decimal? SampleValue);
