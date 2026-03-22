using DeployFlow.Application.Features.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Route("api/notifications")]
[Authorize]
public class NotificationsController : BaseController
{
    public NotificationsController(IMediator mediator) : base(mediator) { }

    /// <summary>Get current email notification preferences.</summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetEmailConfig(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetEmailNotificationConfigQuery(), ct);
        return ToResponse(result);
    }

    /// <summary>Save email notification preferences.</summary>
    [HttpPost("config")]
    public async Task<IActionResult> SaveEmailConfig(
        [FromBody] SaveEmailNotificationRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new UpsertEmailNotificationCommand(
                request.EmailEnabled, request.DeploymentSuccess, request.DeploymentFailure), ct);
        return ToResponse(result);
    }
}

public record SaveEmailNotificationRequest(bool EmailEnabled, bool DeploymentSuccess, bool DeploymentFailure);
