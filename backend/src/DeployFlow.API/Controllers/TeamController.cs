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

    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations(CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new GetTeamInvitationsQuery(), ct));

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteMemberRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new InviteTeamMemberCommand(
            request.Email, request.Name, request.Role), ct));

    [HttpPut("{userId:guid}/role")]
    [HttpPatch("{userId:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid userId, [FromBody] UpdateRoleRequest request, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new UpdateMemberRoleCommand(userId, request.Role), ct));

    [HttpPost("invitations/{invitationId:guid}/resend")]
    public async Task<IActionResult> ResendInvitation(Guid invitationId, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new ResendTeamInvitationCommand(invitationId), ct));

    [HttpDelete("invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid invitationId, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new RevokeTeamInvitationCommand(invitationId), ct));

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, CancellationToken ct = default)
        => ToResponse(await Mediator.Send(new RemoveMemberCommand(userId), ct));
}

public record InviteMemberRequest(string Email, string Name, string Role);
public record UpdateRoleRequest(string Role);
