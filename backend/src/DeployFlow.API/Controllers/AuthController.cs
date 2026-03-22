using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Application.Features.Auth.Commands;
using DeployFlow.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace DeployFlow.API.Controllers;

[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(IMediator mediator, IConfiguration config, UserManager<ApplicationUser> userManager) : base(mediator)
    {
        _config = config;
        _userManager = userManager;
    }

    /// <summary>Login with email and password.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new LoginCommand(request.Email, request.Password, request.TwoFactorCode), ct);
        return ToResponse(result);
    }

    /// <summary>Register a new account and tenant.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new RegisterCommand(request.Name, request.Email, request.Password, request.TenantName), ct);
        return ToResponse(result);
    }

    /// <summary>Refresh the access token using a refresh token.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct);
        return ToResponse(result);
    }

    /// <summary>Logout (client should discard tokens; server invalidates refresh token).</summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // Token invalidation handled by the client and refresh-token expiry
        return NoContent();
    }

    /// <summary>Get the currently authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetMeQuery(), ct);
        return ToResponse(result);
    }

    /// <summary>Update the current user's profile (name, avatar).</summary>
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new UpdateProfileCommand(request.Name, request.AvatarUrl), ct);
        return ToResponse(result);
    }

    /// <summary>Change the current user's password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), ct);
        return ToResponse(result);
    }

    /// <summary>Send a password reset email.</summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new ForgotPasswordCommand(request.Email), ct);
        return ToResponse(result);
    }

    /// <summary>Reset password using the token sent via email.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new ResetPasswordCommand(request.Email, request.Token, request.NewPassword), ct);
        return ToResponse(result);
    }

    /// <summary>Initiate GitHub OAuth sign-in.</summary>
    [HttpGet("github")]
    [EnableRateLimiting("auth")]
    public IActionResult GitHubSignIn()
    {
        var clientId    = _config["GitHub:ClientId"];
        var callbackUrl = _config["GitHub:CallbackUrl"];

        if (string.IsNullOrEmpty(clientId))
            return BadRequest(new { error = "GitHub OAuth is not configured. Set GitHub:ClientId in appsettings." });

        var url = "https://github.com/login/oauth/authorize" +
                  $"?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(callbackUrl ?? "")}" +
                  "&scope=user:email" +
                  $"&state={Guid.NewGuid():N}";

        return Redirect(url);
    }

    /// <summary>Handle GitHub OAuth callback.</summary>
    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken ct)
        => await OAuthCallback(() => Mediator.Send(new GitHubOAuthCommand(code!), ct), code, error);

    // ── Google ──────────────────────────────────────────────────────────────

    /// <summary>Initiate Google OAuth sign-in.</summary>
    [HttpGet("google")]
    [EnableRateLimiting("auth")]
    public IActionResult GoogleSignIn()
    {
        var clientId    = _config["Google:ClientId"];
        var callbackUrl = _config["Google:CallbackUrl"];

        if (string.IsNullOrEmpty(clientId))
            return BadRequest(new { error = "Google OAuth is not configured. Set Google:ClientId in appsettings." });

        var url = "https://accounts.google.com/o/oauth2/v2/auth" +
                  $"?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(callbackUrl ?? "")}" +
                  "&scope=openid+email+profile" +
                  "&response_type=code" +
                  "&access_type=offline" +
                  $"&state={Guid.NewGuid():N}";

        return Redirect(url);
    }

    /// <summary>Handle Google OAuth callback.</summary>
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken ct)
        => await OAuthCallback(() => Mediator.Send(new GoogleOAuthCommand(code!), ct), code, error);

    // ── GitLab ──────────────────────────────────────────────────────────────

    /// <summary>Initiate GitLab OAuth sign-in.</summary>
    [HttpGet("gitlab")]
    [EnableRateLimiting("auth")]
    public IActionResult GitLabSignIn()
    {
        var clientId    = _config["GitLab:ClientId"];
        var callbackUrl = _config["GitLab:CallbackUrl"];
        var baseUrl     = _config["GitLab:BaseUrl"] ?? "https://gitlab.com";

        if (string.IsNullOrEmpty(clientId))
            return BadRequest(new { error = "GitLab OAuth is not configured. Set GitLab:ClientId in appsettings." });

        var url = $"{baseUrl}/oauth/authorize" +
                  $"?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(callbackUrl ?? "")}" +
                  "&scope=read_user" +
                  "&response_type=code" +
                  $"&state={Guid.NewGuid():N}";

        return Redirect(url);
    }

    /// <summary>Handle GitLab OAuth callback.</summary>
    [HttpGet("gitlab/callback")]
    public async Task<IActionResult> GitLabCallback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken ct)
        => await OAuthCallback(() => Mediator.Send(new GitLabOAuthCommand(code!), ct), code, error);

    // ── SSO (OIDC) ───────────────────────────────────────────────────────────

    /// <summary>Initiate SSO sign-in for a workspace.</summary>
    [HttpGet("sso")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SsoSignIn([FromQuery] string workspace, CancellationToken ct)
    {
        var state  = Guid.NewGuid().ToString("N");
        var result = await Mediator.Send(new SsoInitCommand(workspace, state), ct);

        if (!result.IsSuccess)
        {
            var frontendUrl = _config["GitHub:FrontendUrl"] ?? "http://localhost:3003";
            return Redirect($"{frontendUrl}/login?error={Uri.EscapeDataString(result.Error ?? "SSO failed")}");
        }

        return Redirect(result.Value!);
    }

    /// <summary>Handle SSO (OIDC) callback.</summary>
    [HttpGet("sso/callback")]
    public async Task<IActionResult> SsoCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            var fe = _config["GitHub:FrontendUrl"] ?? "http://localhost:3003";
            return Redirect($"{fe}/login?error={Uri.EscapeDataString(error ?? "Missing SSO parameters")}");
        }

        return await OAuthCallback(() => Mediator.Send(new SsoCallbackCommand(code!, state!), ct), code, null);
    }

    // ── Shared helper ────────────────────────────────────────────────────────

    private async Task<IActionResult> OAuthCallback(
        Func<Task<Result<AuthTokensDto>>> handler, string? code, string? error)
    {
        var frontendUrl = _config["GitHub:FrontendUrl"] ?? "http://localhost:3003";

        if (!string.IsNullOrEmpty(error))
            return Redirect($"{frontendUrl}/login?error={Uri.EscapeDataString(error)}");

        if (string.IsNullOrEmpty(code))
            return Redirect($"{frontendUrl}/login?error={Uri.EscapeDataString("Missing authorization code")}");

        var result = await handler();

        if (!result.IsSuccess)
            return Redirect($"{frontendUrl}/login?error={Uri.EscapeDataString(result.Error ?? "OAuth failed")}");

        var tokens  = result.Value!;
        var userJson = Uri.EscapeDataString(System.Text.Json.JsonSerializer.Serialize(tokens.User,
            new System.Text.Json.JsonSerializerOptions
            { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));

        return Redirect(
            $"{frontendUrl}/oauth/callback" +
            $"?accessToken={Uri.EscapeDataString(tokens.AccessToken)}" +
            $"&refreshToken={Uri.EscapeDataString(tokens.RefreshToken)}" +
            $"&user={userJson}");
    }

    // ── Two-Factor Authentication ────────────────────────────────────────────

    /// <summary>Generate a new TOTP authenticator key and return the QR code URI.</summary>
    [HttpGet("2fa/setup")]
    [Authorize]
    public async Task<IActionResult> TwoFaSetup()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        await _userManager.ResetAuthenticatorKeyAsync(user);
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (key is null) return StatusCode(500, new { error = "Failed to generate authenticator key." });

        // Format key as groups of 4 for readability
        var formattedKey = string.Join(" ",
            System.Text.RegularExpressions.Regex.Matches(key.ToUpperInvariant(), ".{1,4}")
                .Select(m => m.Value));

        var issuer  = "DeployFlow";
        var account = Uri.EscapeDataString(user.Email ?? user.UserName ?? "user");
        var issuerE = Uri.EscapeDataString(issuer);
        var qrUri   = $"otpauth://totp/{issuerE}:{account}?secret={key}&issuer={issuerE}&algorithm=SHA1&digits=6&period=30";

        return Ok(new { secret = formattedKey, qrCodeUri = qrUri, email = user.Email });
    }

    /// <summary>Verify the TOTP code and enable 2FA for the current user.</summary>
    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<IActionResult> TwoFaEnable([FromBody] Enable2FaRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var provider  = _userManager.Options.Tokens.AuthenticatorTokenProvider;
        var isValid   = await _userManager.VerifyTwoFactorTokenAsync(user, provider, request.Code.Replace(" ", "").Replace("-", ""));

        if (!isValid)
            return BadRequest(new { error = "Invalid verification code. Please try again." });

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        return Ok(new { success = true, message = "Two-factor authentication has been enabled." });
    }

    /// <summary>Disable 2FA for the current user.</summary>
    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> TwoFaDisable()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        return Ok(new { success = true, message = "Two-factor authentication has been disabled." });
    }
}
