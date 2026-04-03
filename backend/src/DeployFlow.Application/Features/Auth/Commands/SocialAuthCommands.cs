using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DeployFlow.Application.Features.Auth.Commands;

// ─── Google OAuth ─────────────────────────────────────────────────────────────

public record GoogleOAuthCommand(string Code) : IRequest<Result<AuthTokensDto>>;

public class GoogleOAuthCommandHandler : IRequestHandler<GoogleOAuthCommand, Result<AuthTokensDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public GoogleOAuthCommandHandler(
        UserManager<ApplicationUser> userManager, IUnitOfWork uow,
        IJwtService jwt, IConfiguration config, IHttpClientFactory http)
    { _userManager = userManager; _uow = uow; _jwt = jwt; _config = config; _http = http; }

    public async Task<Result<AuthTokensDto>> Handle(GoogleOAuthCommand request, CancellationToken ct)
    {
        // 1. Exchange code → access token
        using var http = _http.CreateClient();
        var tokenResp = await http.PostAsJsonAsync("https://oauth2.googleapis.com/token", new
        {
            client_id     = _config["Google:ClientId"],
            client_secret = _config["Google:ClientSecret"],
            code          = request.Code,
            redirect_uri  = _config["Google:CallbackUrl"],
            grant_type    = "authorization_code",
        }, ct);

        if (!tokenResp.IsSuccessStatusCode)
            return Result<AuthTokensDto>.Failure("Failed to exchange Google code for token.", 400);

        var tokenJson  = await tokenResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var accessToken = tokenJson.TryGetProperty("access_token", out var atEl) ? atEl.GetString() : null;
        if (string.IsNullOrEmpty(accessToken))
            return Result<AuthTokensDto>.Failure("No access token in Google response.", 400);

        // 2. Fetch Google user profile
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        var profile = await http.GetFromJsonAsync<JsonElement>("https://www.googleapis.com/oauth2/v3/userinfo", ct);

        var googleId = profile.TryGetProperty("sub",     out var sEl) ? sEl.GetString() ?? "" : "";
        var email    = profile.TryGetProperty("email",   out var eEl) ? eEl.GetString() ?? "" : "";
        var name     = profile.TryGetProperty("name",    out var nEl) ? nEl.GetString() ?? email : email;
        var avatar   = profile.TryGetProperty("picture", out var pEl) ? pEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(email))
            return Result<AuthTokensDto>.Failure("Could not retrieve email from Google.", 400);

        return await FindOrCreateUserAsync("Google", googleId, email, name, avatar, ct);
    }

    private async Task<Result<AuthTokensDto>> FindOrCreateUserAsync(
        string provider, string providerId, string email, string name, string avatar, CancellationToken ct)
    {
        var user = await _userManager.FindByLoginAsync(provider, providerId);

        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(email);
            if (user is not null)
            {
                await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerId, provider));
                user.AvatarUrl = avatar;
            }
            else
            {
                await _uow.BeginTransactionAsync(ct);
                var tenant = Tenant.Create(name);
                await _uow.Tenants.AddAsync(tenant, ct);
                await _uow.SaveChangesAsync(ct);

                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(), UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email, NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true, FullName = name, AvatarUrl = avatar,
                    TenantId = tenant.Id, Role = "owner",
                    SecurityStamp = Guid.NewGuid().ToString(), IsActive = true,
                };

                var r = await _userManager.CreateAsync(user);
                if (!r.Succeeded)
                {
                    await _uow.RollbackTransactionAsync(ct);
                    return Result<AuthTokensDto>.Failure(string.Join("; ", r.Errors.Select(e => e.Description)));
                }
                await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerId, provider));
                await _uow.CommitTransactionAsync(ct);
            }
        }
        else { user.AvatarUrl = avatar; }

        user.LastLoginAt = DateTime.UtcNow;
        var tenantObj = await _uow.Tenants.GetByIdAsync(user.TenantId, ct);
        var tokens = _jwt.GenerateTokens(user, tenantObj ?? new Tenant());
        user.SetRefreshToken(tokens.RefreshToken, DateTime.UtcNow.AddDays(30));
        await _userManager.UpdateAsync(user);
        return Result<AuthTokensDto>.Success(tokens);
    }
}

// ─── GitLab OAuth ─────────────────────────────────────────────────────────────

public record GitLabOAuthCommand(string Code) : IRequest<Result<AuthTokensDto>>;

public class GitLabOAuthCommandHandler : IRequestHandler<GitLabOAuthCommand, Result<AuthTokensDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public GitLabOAuthCommandHandler(
        UserManager<ApplicationUser> userManager, IUnitOfWork uow,
        IJwtService jwt, IConfiguration config, IHttpClientFactory http)
    { _userManager = userManager; _uow = uow; _jwt = jwt; _config = config; _http = http; }

    public async Task<Result<AuthTokensDto>> Handle(GitLabOAuthCommand request, CancellationToken ct)
    {
        var baseUrl = _config["GitLab:BaseUrl"] ?? "https://gitlab.com";

        using var http = _http.CreateClient();
        var tokenResp = await http.PostAsJsonAsync($"{baseUrl}/oauth/token", new
        {
            client_id     = _config["GitLab:ClientId"],
            client_secret = _config["GitLab:ClientSecret"],
            code          = request.Code,
            redirect_uri  = _config["GitLab:CallbackUrl"],
            grant_type    = "authorization_code",
        }, ct);

        if (!tokenResp.IsSuccessStatusCode)
            return Result<AuthTokensDto>.Failure("Failed to exchange GitLab code for token.", 400);

        var tokenJson   = await tokenResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var accessToken = tokenJson.TryGetProperty("access_token", out var atEl) ? atEl.GetString() : null;
        if (string.IsNullOrEmpty(accessToken))
            return Result<AuthTokensDto>.Failure("No access token in GitLab response.", 400);

        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        var profile  = await http.GetFromJsonAsync<JsonElement>($"{baseUrl}/api/v4/user", ct);
        var glId     = profile.TryGetProperty("id",         out var iEl) ? iEl.GetInt64().ToString() : "";
        var email    = profile.TryGetProperty("email",      out var eEl) ? eEl.GetString() ?? "" : "";
        var name     = profile.TryGetProperty("name",       out var nEl) ? nEl.GetString() ?? email : email;
        var avatar   = profile.TryGetProperty("avatar_url", out var aEl) ? aEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(email))
            return Result<AuthTokensDto>.Failure("Could not retrieve email from GitLab.", 400);

        return await FindOrCreateUserAsync("GitLab", glId, email, name, avatar, ct);
    }

    private async Task<Result<AuthTokensDto>> FindOrCreateUserAsync(
        string provider, string providerId, string email, string name, string avatar, CancellationToken ct)
    {
        var user = await _userManager.FindByLoginAsync(provider, providerId);

        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(email);
            if (user is not null)
            {
                await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerId, provider));
                user.AvatarUrl = avatar;
            }
            else
            {
                await _uow.BeginTransactionAsync(ct);
                var tenant = Tenant.Create(name);
                await _uow.Tenants.AddAsync(tenant, ct);
                await _uow.SaveChangesAsync(ct);

                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(), UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email, NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true, FullName = name, AvatarUrl = avatar,
                    TenantId = tenant.Id, Role = "owner",
                    SecurityStamp = Guid.NewGuid().ToString(), IsActive = true,
                };

                var r = await _userManager.CreateAsync(user);
                if (!r.Succeeded)
                {
                    await _uow.RollbackTransactionAsync(ct);
                    return Result<AuthTokensDto>.Failure(string.Join("; ", r.Errors.Select(e => e.Description)));
                }
                await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerId, provider));
                await _uow.CommitTransactionAsync(ct);
            }
        }
        else { user.AvatarUrl = avatar; }

        user.LastLoginAt = DateTime.UtcNow;
        var tenantObj = await _uow.Tenants.GetByIdAsync(user.TenantId, ct);
        var tokens = _jwt.GenerateTokens(user, tenantObj ?? new Tenant());
        user.SetRefreshToken(tokens.RefreshToken, DateTime.UtcNow.AddDays(30));
        await _userManager.UpdateAsync(user);
        return Result<AuthTokensDto>.Success(tokens);
    }
}

// ─── SSO (OIDC) ───────────────────────────────────────────────────────────────

/// <summary>Returns the OIDC authorization URL for the given workspace.</summary>
public record SsoInitCommand(string Workspace, string State) : IRequest<Result<string>>;

public class SsoInitCommandHandler : IRequestHandler<SsoInitCommand, Result<string>>
{
    private readonly ITenantEntityRepository _tenants;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public SsoInitCommandHandler(ITenantEntityRepository tenants, IConfiguration config, IHttpClientFactory http)
    { _tenants = tenants; _config = config; _http = http; }

    public async Task<Result<string>> Handle(SsoInitCommand request, CancellationToken ct)
    {
        var tenant = await _tenants.GetBySlugAsync(request.Workspace, ct);
        if (tenant is null)
            return Result<string>.Failure("Workspace not found. Check the workspace identifier.", 404);

        if (!tenant.SsoEnabled || string.IsNullOrEmpty(tenant.SsoIssuer) || string.IsNullOrEmpty(tenant.SsoClientId))
            return Result<string>.Failure("SSO is not configured for this workspace.", 400);

        // Discover OIDC endpoints
        using var http = _http.CreateClient();
        http.DefaultRequestHeaders.Add("User-Agent", "DeployFlow");
        var discovery = await http.GetFromJsonAsync<JsonElement>(
            $"{tenant.SsoIssuer.TrimEnd('/')}/.well-known/openid-configuration", ct);

        if (!discovery.TryGetProperty("authorization_endpoint", out var authEl))
            return Result<string>.Failure("Could not discover OIDC authorization endpoint.", 400);

        var callbackUrl = _config["GitHub:FrontendUrl"] is { } fe
            ? $"{fe.TrimEnd('/')}"
            : "http://localhost:3001";

        // Encode workspace + state into the state param
        var encodedState = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{request.Workspace}|{request.State}"));

        var authUrl = authEl.GetString()!
            + $"?client_id={Uri.EscapeDataString(tenant.SsoClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString("http://localhost:5000/api/auth/sso/callback")}"
            + "&scope=openid+email+profile"
            + "&response_type=code"
            + $"&state={Uri.EscapeDataString(encodedState)}";

        return Result<string>.Success(authUrl);
    }
}

/// <summary>Exchanges the SSO code for a DeployFlow JWT.</summary>
public record SsoCallbackCommand(string Code, string State) : IRequest<Result<AuthTokensDto>>;

public class SsoCallbackCommandHandler : IRequestHandler<SsoCallbackCommand, Result<AuthTokensDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;
    private readonly IHttpClientFactory _http;

    public SsoCallbackCommandHandler(
        UserManager<ApplicationUser> userManager, IUnitOfWork uow,
        IJwtService jwt, IHttpClientFactory http)
    { _userManager = userManager; _uow = uow; _jwt = jwt; _http = http; }

    public async Task<Result<AuthTokensDto>> Handle(SsoCallbackCommand request, CancellationToken ct)
    {
        // Decode state → workspace slug
        string workspace;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(request.State));
            workspace = decoded.Split('|')[0];
        }
        catch { return Result<AuthTokensDto>.Failure("Invalid SSO state.", 400); }

        var tenant = await _uow.Tenants.FindAsync(t => t.Slug == workspace, ct);
        var tenantObj = tenant.FirstOrDefault();
        if (tenantObj is null)
            return Result<AuthTokensDto>.Failure("Workspace not found.", 404);

        if (string.IsNullOrEmpty(tenantObj.SsoIssuer) || string.IsNullOrEmpty(tenantObj.SsoClientId))
            return Result<AuthTokensDto>.Failure("SSO not configured for this workspace.", 400);

        using var http = _http.CreateClient();
        http.DefaultRequestHeaders.Add("User-Agent", "DeployFlow");

        // Discover token + userinfo endpoints
        var discovery = await http.GetFromJsonAsync<JsonElement>(
            $"{tenantObj.SsoIssuer.TrimEnd('/')}/.well-known/openid-configuration", ct);

        if (!discovery.TryGetProperty("token_endpoint",    out var tokenEl) ||
            !discovery.TryGetProperty("userinfo_endpoint", out var userInfoEl))
            return Result<AuthTokensDto>.Failure("Could not discover OIDC endpoints.", 400);

        // Exchange code → token
        var tokenResp = await http.PostAsJsonAsync(tokenEl.GetString(), new
        {
            client_id     = tenantObj.SsoClientId,
            client_secret = tenantObj.SsoClientSecret ?? "",
            code          = request.Code,
            redirect_uri  = "http://localhost:5000/api/auth/sso/callback",
            grant_type    = "authorization_code",
        }, ct);

        if (!tokenResp.IsSuccessStatusCode)
            return Result<AuthTokensDto>.Failure("Failed to exchange SSO code for token.", 400);

        var tokenJson   = await tokenResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var accessToken = tokenJson.TryGetProperty("access_token", out var atEl) ? atEl.GetString() : null;
        if (string.IsNullOrEmpty(accessToken))
            return Result<AuthTokensDto>.Failure("No access token in SSO response.", 400);

        // Get user info
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        var profile = await http.GetFromJsonAsync<JsonElement>(userInfoEl.GetString()!, ct);

        var sub    = profile.TryGetProperty("sub",     out var sEl) ? sEl.GetString() ?? "" : "";
        var email  = profile.TryGetProperty("email",   out var eEl) ? eEl.GetString() ?? "" : "";
        var name   = profile.TryGetProperty("name",    out var nEl) ? nEl.GetString() ?? email : email;
        var avatar = profile.TryGetProperty("picture", out var pEl) ? pEl.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(email))
            return Result<AuthTokensDto>.Failure("Could not retrieve email from SSO provider.", 400);

        var providerKey = $"sso:{tenantObj.Slug}:{sub}";
        var user = await _userManager.FindByLoginAsync("SSO", providerKey);

        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(email);
            if (user is not null)
            {
                await _userManager.AddLoginAsync(user, new UserLoginInfo("SSO", providerKey, "SSO"));
                user.AvatarUrl = avatar;
            }
            else
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(), UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email, NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true, FullName = name, AvatarUrl = avatar,
                    TenantId = tenantObj.Id, Role = "developer",
                    SecurityStamp = Guid.NewGuid().ToString(), IsActive = true,
                };

                var r = await _userManager.CreateAsync(user);
                if (!r.Succeeded)
                    return Result<AuthTokensDto>.Failure(string.Join("; ", r.Errors.Select(e => e.Description)));

                await _userManager.AddLoginAsync(user, new UserLoginInfo("SSO", providerKey, "SSO"));
            }
        }
        else { user.AvatarUrl = avatar; }

        user.LastLoginAt = DateTime.UtcNow;
        var tokens = _jwt.GenerateTokens(user, tenantObj);
        user.SetRefreshToken(tokens.RefreshToken, DateTime.UtcNow.AddDays(30));
        await _userManager.UpdateAsync(user);
        return Result<AuthTokensDto>.Success(tokens);
    }
}
