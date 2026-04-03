using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace DeployFlow.Application.Features.Team;

// ─── Get Team Members ─────────────────────────────────────────────────────────

public record GetTeamMembersQuery : IRequest<Result<List<TeamMemberDto>>>;
public record GetTeamInvitationsQuery : IRequest<Result<List<TeamInvitationDto>>>;

public class GetTeamMembersQueryHandler : IRequestHandler<GetTeamMembersQuery, Result<List<TeamMemberDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetTeamMembersQueryHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<List<TeamMemberDto>>> Handle(GetTeamMembersQuery request, CancellationToken ct)
    {
        var members = await _uow.Users.GetByTenantAsync(_currentUser.TenantId, ct);
        var dtos = members.Select(m => new TeamMemberDto(
            m.Id, m.FullName, m.Email ?? "", m.Role, m.AvatarUrl ?? "",
            m.IsActive ? "active" : "inactive", m.LastLoginAt, m.CreatedAt)).ToList();
        return Result<List<TeamMemberDto>>.Success(dtos);
    }
}

public class GetTeamInvitationsQueryHandler : IRequestHandler<GetTeamInvitationsQuery, Result<List<TeamInvitationDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetTeamInvitationsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<TeamInvitationDto>>> Handle(GetTeamInvitationsQuery request, CancellationToken ct)
    {
        var invites = await _uow.TeamInvitations.GetByTenantAsync(_currentUser.TenantId, ct);
        var dtos = invites
            .OrderByDescending(i => i.CreatedAt)
            .Select(TeamInvitationMappings.Map)
            .ToList();
        return Result<List<TeamInvitationDto>>.Success(dtos);
    }
}

// ─── Invite Team Member ───────────────────────────────────────────────────────

public record InviteTeamMemberCommand(
    string Email,
    string Name,
    string Role
) : IRequest<Result<TeamInvitationDto>>;

public class InviteTeamMemberCommandValidator : AbstractValidator<InviteTeamMemberCommand>
{
    public InviteTeamMemberCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role)
            .Must(r => new[] { "owner", "admin", "devops", "developer", "viewer" }.Contains(r.ToLower()))
            .WithMessage("Role must be: owner, admin, devops, developer, or viewer.");
    }
}

public class InviteTeamMemberCommandHandler : IRequestHandler<InviteTeamMemberCommand, Result<TeamInvitationDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;

    public InviteTeamMemberCommandHandler(
        UserManager<ApplicationUser> um, IUnitOfWork uow, ICurrentUser cu, IEmailService email, IAuditService auditService, IConfiguration configuration)
    { _userManager = um; _uow = uow; _currentUser = cu; _emailService = email; _auditService = auditService; _configuration = configuration; }

    public async Task<Result<TeamInvitationDto>> Handle(InviteTeamMemberCommand request, CancellationToken ct)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null && existing.TenantId == _currentUser.TenantId)
            return Result<TeamInvitationDto>.Failure("This user is already a member of your team.");

        var invitations = await _uow.TeamInvitations.GetByTenantAsync(_currentUser.TenantId, ct);
        var existingInvite = invitations.FirstOrDefault(i =>
            i.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase));

        if (existingInvite is not null && !existingInvite.IsExpired)
            return Result<TeamInvitationDto>.Failure("A pending invitation already exists for this email.");

        TeamInvitation invitation;
        if (existingInvite is not null)
        {
            existingInvite.Resend();
            invitation = existingInvite;
        }
        else
        {
            invitation = TeamInvitation.Create(
                _currentUser.TenantId,
                request.Email,
                request.Name,
                request.Role,
                _currentUser.UserId,
                _currentUser.Name);
            await _uow.TeamInvitations.AddAsync(invitation, ct);
        }

        var frontendUrl = _configuration["GitHub:FrontendUrl"] ?? "http://localhost:3000";
        var inviteUrl = $"{frontendUrl}/accept-invitation?token={Uri.EscapeDataString(invitation.Token)}";

        await _emailService.SendAsync(request.Email,
            "You've been invited to DeployFlow",
            $"<p>{_currentUser.Name} invited you to join DeployFlow as <strong>{request.Role}</strong>.</p><p>This invitation expires on <strong>{invitation.ExpiresAt:yyyy-MM-dd HH:mm} UTC</strong>.</p><p><a href=\"{inviteUrl}\">Accept invitation</a></p>",
            ct);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.Name,
            "team.invitation.sent",
            "team-invitation",
            invitation.Id,
            request.Email,
            _currentUser.TenantId,
            metadata: new { request.Role, invitation.ExpiresAt },
            ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Result<TeamInvitationDto>.Success(TeamInvitationMappings.Map(invitation));
    }
}

public record ResendTeamInvitationCommand(Guid InvitationId) : IRequest<Result<TeamInvitationDto>>;

public class ResendTeamInvitationCommandHandler : IRequestHandler<ResendTeamInvitationCommand, Result<TeamInvitationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;

    public ResendTeamInvitationCommandHandler(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IEmailService emailService,
        IAuditService auditService,
        IConfiguration configuration)
    {
        _uow = uow;
        _currentUser = currentUser;
        _emailService = emailService;
        _auditService = auditService;
        _configuration = configuration;
    }

    public async Task<Result<TeamInvitationDto>> Handle(ResendTeamInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _uow.TeamInvitations.GetByIdAsync(request.InvitationId, ct);
        if (invitation is null || invitation.TenantId != _currentUser.TenantId)
            return Result<TeamInvitationDto>.Failure("Invitation not found.", 404);

        invitation.Resend();

        var frontendUrl = _configuration["GitHub:FrontendUrl"] ?? "http://localhost:3000";
        var inviteUrl = $"{frontendUrl}/accept-invitation?token={Uri.EscapeDataString(invitation.Token)}";

        await _emailService.SendAsync(
            invitation.Email,
            "Your DeployFlow invitation has been renewed",
            $"<p>{_currentUser.Name} resent your invitation to join DeployFlow as <strong>{invitation.Role}</strong>.</p><p>This invitation expires on <strong>{invitation.ExpiresAt:yyyy-MM-dd HH:mm} UTC</strong>.</p><p><a href=\"{inviteUrl}\">Accept invitation</a></p>",
            ct);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.Name,
            "team.invitation.resent",
            "team-invitation",
            invitation.Id,
            invitation.Email,
            _currentUser.TenantId,
            metadata: new { invitation.ResendCount, invitation.ExpiresAt },
            ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Result<TeamInvitationDto>.Success(TeamInvitationMappings.Map(invitation));
    }
}

public record RevokeTeamInvitationCommand(Guid InvitationId) : IRequest<Result>;

public class RevokeTeamInvitationCommandHandler : IRequestHandler<RevokeTeamInvitationCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public RevokeTeamInvitationCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IAuditService auditService)
    {
        _uow = uow;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(RevokeTeamInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _uow.TeamInvitations.GetByIdAsync(request.InvitationId, ct);
        if (invitation is null || invitation.TenantId != _currentUser.TenantId)
            return Result.Failure("Invitation not found.", 404);

        await _uow.TeamInvitations.DeleteAsync(invitation, ct);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.Name,
            "team.invitation.revoked",
            "team-invitation",
            invitation.Id,
            invitation.Email,
            _currentUser.TenantId,
            ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Update Member Role ────────────────────────────────────────────────────────

public record UpdateMemberRoleCommand(Guid UserId, string Role) : IRequest<Result>;

public class UpdateMemberRoleCommandValidator : AbstractValidator<UpdateMemberRoleCommand>
{
    public UpdateMemberRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role)
            .Must(r => new[] { "owner", "admin", "devops", "developer", "viewer" }.Contains(r.ToLower()))
            .WithMessage("Role must be: owner, admin, devops, developer, or viewer.");
    }
}

public class UpdateMemberRoleCommandHandler : IRequestHandler<UpdateMemberRoleCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public UpdateMemberRoleCommandHandler(UserManager<ApplicationUser> um, ICurrentUser cu)
    { _userManager = um; _currentUser = cu; }

    public async Task<Result> Handle(UpdateMemberRoleCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.TenantId != _currentUser.TenantId)
            return Result.Failure("User not found.", 404);

        if (user.Id == _currentUser.UserId)
            return Result.Failure("Cannot change your own role.");

        user.Role = request.Role.ToLower();
        await _userManager.UpdateAsync(user);
        return Result.Success();
    }
}

// ─── Remove Member ─────────────────────────────────────────────────────────────

public record RemoveMemberCommand(Guid UserId) : IRequest<Result>;

public class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public RemoveMemberCommandHandler(UserManager<ApplicationUser> um, ICurrentUser cu)
    { _userManager = um; _currentUser = cu; }

    public async Task<Result> Handle(RemoveMemberCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.TenantId != _currentUser.TenantId)
            return Result.Failure("User not found.", 404);

        if (user.Id == _currentUser.UserId)
            return Result.Failure("Cannot remove yourself from the team.");

        user.IsActive = false;
        await _userManager.UpdateAsync(user);
        return Result.Success();
    }
}

internal static class TeamInvitationMappings
{
    public static TeamInvitationDto Map(TeamInvitation invitation)
        => new(
            invitation.Id,
            invitation.Email,
            invitation.Name,
            invitation.Role,
            invitation.IsExpired ? "expired" : "pending",
            invitation.ExpiresAt,
            invitation.LastSentAt,
            invitation.ResendCount,
            invitation.InvitedByName,
            invitation.CreatedAt);
}
