using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace DeployFlow.Application.Features.Auth.Commands;

// ─── Register ────────────────────────────────────────────────────────────────

public record RegisterCommand(
    string Name,
    string Email,
    string Password,
    string? TenantName
) : IRequest<Result<AuthTokensDto>>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.");
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthTokensDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;

    public RegisterCommandHandler(UserManager<ApplicationUser> userManager, IUnitOfWork uow, IJwtService jwt)
    { _userManager = userManager; _uow = uow; _jwt = jwt; }

    public async Task<Result<AuthTokensDto>> Handle(RegisterCommand request, CancellationToken ct)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Result<AuthTokensDto>.Failure("An account with this email already exists.", 400);

        await _uow.BeginTransactionAsync(ct);

        var tenant = Tenant.Create(request.TenantName ?? request.Name);
        await _uow.Tenants.AddAsync(tenant, ct);
        await _uow.SaveChangesAsync(ct);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            NormalizedUserName = request.Email.ToUpperInvariant(),
            Email = request.Email,
            NormalizedEmail = request.Email.ToUpperInvariant(),
            FullName = request.Name,
            TenantId = tenant.Id,
            Role = "owner",
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<AuthTokensDto>.Failure(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await _uow.CommitTransactionAsync(ct);

        var tokens = _jwt.GenerateTokens(user, tenant);
        user.SetRefreshToken(tokens.RefreshToken, DateTime.UtcNow.AddDays(30));
        await _userManager.UpdateAsync(user);

        return Result<AuthTokensDto>.Success(tokens);
    }
}

// ─── Login ────────────────────────────────────────────────────────────────────

public record LoginCommand(
    string Email,
    string Password,
    string? TwoFactorCode
) : IRequest<Result<AuthTokensDto>>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthTokensDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;

    public LoginCommandHandler(UserManager<ApplicationUser> userManager, IUnitOfWork uow, IJwtService jwt)
    { _userManager = userManager; _uow = uow; _jwt = jwt; }

    public async Task<Result<AuthTokensDto>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Result<AuthTokensDto>.Failure("Invalid credentials.", 401);

        var valid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!valid)
            return Result<AuthTokensDto>.Failure("Invalid credentials.", 401);

        if (user.TwoFactorEnabled && string.IsNullOrEmpty(request.TwoFactorCode))
            return Result<AuthTokensDto>.Failure("Two-factor code required.", 403);

        var tenant = await _uow.Tenants.GetByIdAsync(user.TenantId, ct);
        user.LastLoginAt = DateTime.UtcNow;

        var tokens = _jwt.GenerateTokens(user, tenant ?? new Tenant());
        user.SetRefreshToken(tokens.RefreshToken, DateTime.UtcNow.AddDays(30));
        await _userManager.UpdateAsync(user);

        return Result<AuthTokensDto>.Success(tokens);
    }
}

// ─── Refresh Token ────────────────────────────────────────────────────────────

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokensDto>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;

    public RefreshTokenCommandHandler(UserManager<ApplicationUser> manager, IUnitOfWork uow, IJwtService jwt)
    { _userManager = manager; _uow = uow; _jwt = jwt; }

    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByRefreshTokenAsync(request.RefreshToken, ct);
        if (user is null || user.RefreshTokenExpiry < DateTime.UtcNow)
            return Result<AuthTokensDto>.Failure("Invalid or expired refresh token.", 401);

        var tenant = await _uow.Tenants.GetByIdAsync(user.TenantId, ct);

        var tokens = _jwt.GenerateTokens(user, tenant ?? new Tenant());
        user.SetRefreshToken(tokens.RefreshToken, DateTime.UtcNow.AddDays(30));
        await _userManager.UpdateAsync(user);

        return Result<AuthTokensDto>.Success(tokens);
    }
}

// ─── Logout ───────────────────────────────────────────────────────────────────

public record LogoutCommand : IRequest<Result>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public LogoutCommandHandler(UserManager<ApplicationUser> mgr, ICurrentUser cu)
    { _userManager = mgr; _currentUser = cu; }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(_currentUser.UserId.ToString());
        if (user is not null)
        {
            user.SetRefreshToken("", DateTime.UtcNow.AddSeconds(-1));
            await _userManager.UpdateAsync(user);
        }
        return Result.Success();
    }
}

// ─── Change Password ──────────────────────────────────────────────────────────

public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword
) : IRequest<Result>;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public ChangePasswordCommandHandler(UserManager<ApplicationUser> mgr, ICurrentUser cu)
    { _userManager = mgr; _currentUser = cu; }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(_currentUser.UserId.ToString());
        if (user is null) return Result.Failure("User not found.", 404);

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return Result.Failure(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Result.Success();
    }
}

// ─── GitHub OAuth ──────────────────────────────────────────────────────────────

public record GitHubOAuthCommand(string Code) : IRequest<Result<AuthTokensDto>>;

public class GitHubOAuthCommandHandler : IRequestHandler<GitHubOAuthCommand, Result<AuthTokensDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public GitHubOAuthCommandHandler(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork uow,
        IJwtService jwt,
        IConfiguration config,
        IHttpClientFactory http)
    {
        _userManager = userManager;
        _uow = uow;
        _jwt = jwt;
        _config = config;
        _http = http;
    }

    public async Task<Result<AuthTokensDto>> Handle(GitHubOAuthCommand request, CancellationToken ct)
    {
        // 1. Exchange GitHub code → access token
        var ghAccessToken = await ExchangeCodeAsync(request.Code, ct);
        if (ghAccessToken is null)
            return Result<AuthTokensDto>.Failure("Failed to exchange GitHub code for access token.", 400);

        // 2. Fetch GitHub user profile
        using var gh = _http.CreateClient();
        gh.DefaultRequestHeaders.Add("Authorization", $"Bearer {ghAccessToken}");
        gh.DefaultRequestHeaders.Add("User-Agent", "DeployFlow");
        gh.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

        var profile = await gh.GetFromJsonAsync<JsonElement>("https://api.github.com/user", ct);
        var ghId    = profile.GetProperty("id").GetInt64().ToString();
        var ghLogin = profile.TryGetProperty("login",      out var lEl) ? lEl.GetString() ?? "" : "";
        var ghName  = profile.TryGetProperty("name",       out var nEl) && nEl.ValueKind != JsonValueKind.Null
                          ? nEl.GetString() ?? ghLogin : ghLogin;
        var ghAvatar = profile.TryGetProperty("avatar_url", out var aEl) ? aEl.GetString() ?? "" : "";
        var ghEmail  = profile.TryGetProperty("email",      out var eEl) && eEl.ValueKind != JsonValueKind.Null
                          ? eEl.GetString() : null;

        // If email is private, ask the emails endpoint
        if (string.IsNullOrEmpty(ghEmail))
        {
            try
            {
                var emails = await gh.GetFromJsonAsync<JsonElement[]>("https://api.github.com/user/emails", ct);
                if (emails != null)
                {
                    foreach (var entry in emails)
                    {
                        bool primary   = entry.TryGetProperty("primary",  out var pEl) && pEl.GetBoolean();
                        bool verified  = entry.TryGetProperty("verified", out var vEl) && vEl.GetBoolean();
                        if (primary && verified && entry.TryGetProperty("email", out var emEl))
                        { ghEmail = emEl.GetString(); break; }
                    }
                    if (string.IsNullOrEmpty(ghEmail) && emails.Length > 0 &&
                        emails[0].TryGetProperty("email", out var fallback))
                        ghEmail = fallback.GetString();
                }
            }
            catch { /* email remains null */ }
        }

        if (string.IsNullOrEmpty(ghEmail))
            return Result<AuthTokensDto>.Failure(
                "Could not retrieve email from GitHub. Make sure your email is visible or primary.", 400);

        // 3. Find or create ApplicationUser
        const string provider = "GitHub";
        var user = await _userManager.FindByLoginAsync(provider, ghId);

        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(ghEmail);
            if (user is not null)
            {
                // Link GitHub to existing email account
                await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, ghId, "GitHub"));
                user.AvatarUrl = ghAvatar;
            }
            else
            {
                // New user: create tenant + account
                await _uow.BeginTransactionAsync(ct);
                var tenant = Tenant.Create(ghLogin.Length > 0 ? ghLogin : ghEmail);
                await _uow.Tenants.AddAsync(tenant, ct);
                await _uow.SaveChangesAsync(ct);

                user = new ApplicationUser
                {
                    Id                   = Guid.NewGuid(),
                    UserName             = ghEmail,
                    NormalizedUserName   = ghEmail.ToUpperInvariant(),
                    Email                = ghEmail,
                    NormalizedEmail      = ghEmail.ToUpperInvariant(),
                    EmailConfirmed       = true,
                    FullName             = ghName,
                    AvatarUrl            = ghAvatar,
                    TenantId             = tenant.Id,
                    Role                 = "owner",
                    SecurityStamp        = Guid.NewGuid().ToString(),
                    IsActive             = true,
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    await _uow.RollbackTransactionAsync(ct);
                    return Result<AuthTokensDto>.Failure(
                        string.Join("; ", createResult.Errors.Select(e => e.Description)));
                }

                await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, ghId, "GitHub"));
                await _uow.CommitTransactionAsync(ct);
            }
        }
        else
        {
            user.AvatarUrl = ghAvatar;
        }

        user.LastLoginAt = DateTime.UtcNow;
        var tenantObj = await _uow.Tenants.GetByIdAsync(user.TenantId, ct);
        var tokens = _jwt.GenerateTokens(user, tenantObj ?? new Tenant());
        user.SetRefreshToken(tokens.RefreshToken, DateTime.UtcNow.AddDays(30));
        await _userManager.UpdateAsync(user);

        return Result<AuthTokensDto>.Success(tokens);
    }

    private async Task<string?> ExchangeCodeAsync(string code, CancellationToken ct)
    {
        using var http = _http.CreateClient();
        http.DefaultRequestHeaders.Add("Accept", "application/json");
        http.DefaultRequestHeaders.Add("User-Agent", "DeployFlow");

        var response = await http.PostAsJsonAsync(
            "https://github.com/login/oauth/access_token",
            new
            {
                client_id     = _config["GitHub:ClientId"],
                client_secret = _config["GitHub:ClientSecret"],
                code,
                redirect_uri  = _config["GitHub:CallbackUrl"],
            }, ct);

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.TryGetProperty("access_token", out var tokenEl) ? tokenEl.GetString() : null;
    }
}

// ─── Get Current User ─────────────────────────────────────────────────────────

public record GetMeQuery : IRequest<Result<AuthUserDto>>;

public class GetMeQueryHandler : IRequestHandler<GetMeQuery, Result<AuthUserDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetMeQueryHandler(UserManager<ApplicationUser> userManager, IUnitOfWork uow, ICurrentUser currentUser)
    { _userManager = userManager; _uow = uow; _currentUser = currentUser; }

    public async Task<Result<AuthUserDto>> Handle(GetMeQuery request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(_currentUser.UserId.ToString());
        if (user is null) return Result<AuthUserDto>.Failure("User not found.", 404);

        var tenant = await _uow.Tenants.GetByIdAsync(user.TenantId, ct);
        var dto = new AuthUserDto(
            user.Id, user.Email!, user.FullName, user.Role,
            user.AvatarUrl ?? "", user.TenantId,
            tenant?.Name ?? "", tenant?.PlanName ?? "",
            user.TwoFactorEnabled, user.CreatedAt,
            user.IsActive, user.LastLoginAt);

        return Result<AuthUserDto>.Success(dto);
    }
}

// ─── Update Profile ────────────────────────────────────────────────────────────

public record UpdateProfileCommand(string Name, string? AvatarUrl) : IRequest<Result<AuthUserDto>>;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Result<AuthUserDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdateProfileCommandHandler(UserManager<ApplicationUser> userManager, IUnitOfWork uow, ICurrentUser currentUser)
    { _userManager = userManager; _uow = uow; _currentUser = currentUser; }

    public async Task<Result<AuthUserDto>> Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2)
            return Result<AuthUserDto>.Failure("Name must be at least 2 characters.", 400);

        var user = await _userManager.FindByIdAsync(_currentUser.UserId.ToString());
        if (user is null) return Result<AuthUserDto>.Failure("User not found.", 404);

        user.FullName = request.Name.Trim();
        if (request.AvatarUrl is not null)
            user.AvatarUrl = request.AvatarUrl;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Result<AuthUserDto>.Failure(string.Join("; ", result.Errors.Select(e => e.Description)));

        var tenant = await _uow.Tenants.GetByIdAsync(user.TenantId, ct);
        var dto = new AuthUserDto(
            user.Id, user.Email!, user.FullName, user.Role,
            user.AvatarUrl ?? "", user.TenantId,
            tenant?.Name ?? "", tenant?.PlanName ?? "",
            user.TwoFactorEnabled, user.CreatedAt,
            user.IsActive, user.LastLoginAt);

        return Result<AuthUserDto>.Success(dto);
    }
}

// ─── Forgot Password ───────────────────────────────────────────────────────────

public record ForgotPasswordCommand(string Email) : IRequest<Result>;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;

    public ForgotPasswordCommandHandler(UserManager<ApplicationUser> userManager, IEmailService email, IConfiguration config)
    { _userManager = userManager; _email = email; _config = config; }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        // Always return success — do not reveal whether the email exists
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Result.Success();

        var token       = await _userManager.GeneratePasswordResetTokenAsync(user);
        var frontendUrl = _config["GitHub:FrontendUrl"] ?? "http://localhost:3003";
        var resetLink   = $"{frontendUrl}/reset-password" +
                          $"?email={Uri.EscapeDataString(user.Email!)}" +
                          $"&token={Uri.EscapeDataString(token)}";

        await _email.SendTemplateAsync(
            user.Email!,
            "password-reset",
            new Dictionary<string, string>
            {
                ["subject"] = "Reset your DeployFlow password",
                ["link"]    = resetLink,
            },
            ct);

        return Result.Success();
    }
}

// ─── Reset Password ────────────────────────────────────────────────────────────

public record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result>;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ResetPasswordCommandHandler(UserManager<ApplicationUser> userManager)
    { _userManager = userManager; }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure("Invalid password reset request.", 400);

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return Result.Failure(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Result.Success();
    }
}

// ─── Team Invitation Redemption ──────────────────────────────────────────────

public record GetInvitationByTokenQuery(string Token) : IRequest<Result<InvitationPreviewDto>>;

public class GetInvitationByTokenQueryHandler : IRequestHandler<GetInvitationByTokenQuery, Result<InvitationPreviewDto>>
{
    private readonly IUnitOfWork _uow;

    public GetInvitationByTokenQueryHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<InvitationPreviewDto>> Handle(GetInvitationByTokenQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Result<InvitationPreviewDto>.Failure("Invitation token is required.", 400);

        var invitation = (await _uow.TeamInvitations.FindAsync(i => i.Token == request.Token, ct)).FirstOrDefault();
        if (invitation is null)
            return Result<InvitationPreviewDto>.Failure("Invitation not found.", 404);

        if (invitation.IsExpired)
            return Result<InvitationPreviewDto>.Failure("Invitation has expired.", 400);

        return Result<InvitationPreviewDto>.Success(new InvitationPreviewDto(
            invitation.Email,
            invitation.Name,
            invitation.Role,
            invitation.ExpiresAt));
    }
}

public record AcceptInvitationCommand(string Token, string Name, string Password) : IRequest<Result<AuthTokensDto>>;

public class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.");
    }
}

public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, Result<AuthTokensDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;

    public AcceptInvitationCommandHandler(UserManager<ApplicationUser> userManager, IUnitOfWork uow, IJwtService jwt)
    {
        _userManager = userManager;
        _uow = uow;
        _jwt = jwt;
    }

    public async Task<Result<AuthTokensDto>> Handle(AcceptInvitationCommand request, CancellationToken ct)
    {
        var invitation = (await _uow.TeamInvitations.FindAsync(i => i.Token == request.Token, ct)).FirstOrDefault();
        if (invitation is null)
            return Result<AuthTokensDto>.Failure("Invitation not found.", 404);

        if (invitation.IsExpired)
            return Result<AuthTokensDto>.Failure("Invitation has expired.", 400);

        var existing = await _userManager.FindByEmailAsync(invitation.Email);
        if (existing is not null)
            return Result<AuthTokensDto>.Failure("An account with this email already exists.", 400);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = invitation.Email,
            NormalizedUserName = invitation.Email.ToUpperInvariant(),
            Email = invitation.Email,
            NormalizedEmail = invitation.Email.ToUpperInvariant(),
            FullName = request.Name.Trim(),
            TenantId = invitation.TenantId,
            Role = invitation.Role,
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true,
        };

        var create = await _userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded)
            return Result<AuthTokensDto>.Failure(string.Join("; ", create.Errors.Select(e => e.Description)));

        await _uow.TeamInvitations.DeleteAsync(invitation, ct);

        var tenant = await _uow.Tenants.GetByIdAsync(invitation.TenantId, ct);
        var tokens = _jwt.GenerateTokens(user, tenant ?? new Tenant());
        user.SetRefreshToken(tokens.RefreshToken, DateTime.UtcNow.AddDays(30));
        await _userManager.UpdateAsync(user);

        await _uow.SaveChangesAsync(ct);
        return Result<AuthTokensDto>.Success(tokens);
    }
}
