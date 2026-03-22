using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;
using Microsoft.AspNetCore.Identity;

namespace DeployFlow.Application.Features.Team;

// ─── Get Team Members ─────────────────────────────────────────────────────────

public record GetTeamMembersQuery : IRequest<Result<List<TeamMemberDto>>>;

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

// ─── Invite Team Member ───────────────────────────────────────────────────────

public record InviteTeamMemberCommand(
    string Email,
    string Name,
    string Role
) : IRequest<Result<TeamMemberDto>>;

public class InviteTeamMemberCommandValidator : AbstractValidator<InviteTeamMemberCommand>
{
    public InviteTeamMemberCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role)
            .Must(r => new[] { "owner", "admin", "developer", "viewer" }.Contains(r.ToLower()))
            .WithMessage("Role must be: owner, admin, developer, or viewer.");
    }
}

public class InviteTeamMemberCommandHandler : IRequestHandler<InviteTeamMemberCommand, Result<TeamMemberDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IEmailService _emailService;

    public InviteTeamMemberCommandHandler(
        UserManager<ApplicationUser> um, IUnitOfWork uow, ICurrentUser cu, IEmailService email)
    { _userManager = um; _uow = uow; _currentUser = cu; _emailService = email; }

    public async Task<Result<TeamMemberDto>> Handle(InviteTeamMemberCommand request, CancellationToken ct)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null && existing.TenantId == _currentUser.TenantId)
            return Result<TeamMemberDto>.Failure("This user is already a member of your team.");

        if (existing is null)
        {
            // Create user with temp password - they'll reset on first login
            var tempPassword = Guid.NewGuid().ToString("N")[..12] + "A1!";
            var newUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = request.Email,
                NormalizedUserName = request.Email.ToUpperInvariant(),
                Email = request.Email,
                NormalizedEmail = request.Email.ToUpperInvariant(),
                FullName = request.Name,
                TenantId = _currentUser.TenantId,
                Role = request.Role.ToLower(),
                SecurityStamp = Guid.NewGuid().ToString(),
                IsActive = true
            };
            var result = await _userManager.CreateAsync(newUser, tempPassword);
            if (!result.Succeeded)
                return Result<TeamMemberDto>.Failure(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _emailService.SendAsync(request.Email,
                "You've been invited to DeployFlow",
                $"<p>You've been invited to join a team on DeployFlow. Login with your email and temp password: <code>{tempPassword}</code></p>",
                ct);

            return Result<TeamMemberDto>.Success(new TeamMemberDto(
                newUser.Id, newUser.FullName, newUser.Email!, newUser.Role,
                newUser.AvatarUrl ?? "", "active", null, newUser.CreatedAt));
        }

        // Existing user - add to this tenant
        existing.TenantId = _currentUser.TenantId;
        existing.Role = request.Role.ToLower();
        await _userManager.UpdateAsync(existing);

        return Result<TeamMemberDto>.Success(new TeamMemberDto(
            existing.Id, existing.FullName, existing.Email!, existing.Role,
            existing.AvatarUrl ?? "", existing.IsActive ? "active" : "inactive", existing.LastLoginAt, existing.CreatedAt));
    }
}

// ─── Update Member Role ────────────────────────────────────────────────────────

public record UpdateMemberRoleCommand(Guid UserId, string Role) : IRequest<Result>;

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
