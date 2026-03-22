using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Team;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[Authorize]
[Route("api/team")]
public class TeamController : BaseController
{
    public TeamController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetMembers(CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetTeamMembersQuery(), ct));

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteMemberRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new InviteTeamMemberCommand(
            request.Email, request.Name, request.Role), ct));

    [HttpPatch("{userId:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid userId, [FromBody] UpdateRoleRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new UpdateMemberRoleCommand(userId, request.Role), ct));

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new RemoveMemberCommand(userId), ct));
}

public record InviteMemberRequest(string Email, string Name, string Role);
public record UpdateRoleRequest(string Role);
