using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DeployFlow.Infrastructure.Identity;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtService(IConfiguration config)
    {
        _config = config;
        _secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured.");
        _issuer = config["Jwt:Issuer"] ?? "deployflow";
        _audience = config["Jwt:Audience"] ?? "deployflow-users";
        _expiryMinutes = int.TryParse(config["Jwt:ExpiryMinutes"], out var m) ? m : 60;
    }

    public (string AccessToken, DateTime ExpiresAt) GenerateAccessToken(ApplicationUser user, string tenantName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.FullName ?? ""),
            new("tenant_id", user.TenantId.ToString()),
            new("tenant_name", tenantName ?? ""),
            new(ClaimTypes.Role, user.Role ?? "user")
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public AuthTokensDto GenerateTokens(ApplicationUser user, Tenant tenant)
    {
        var (accessToken, expiresAt) = GenerateAccessToken(user, tenant.Name ?? "");
        var refreshToken = GenerateRefreshToken();
        var userDto = new AuthUserDto(
            user.Id, user.Email ?? "", user.FullName ?? "", user.Role ?? "user",
            user.AvatarUrl ?? "", user.TenantId, tenant.Name ?? "", tenant.PlanName,
            user.TwoFactorEnabled, user.CreatedAt, user.IsActive, user.LastLoginAt);
        return new AuthTokensDto(accessToken, refreshToken, expiresAt, userDto);
    }

    public Guid? ValidateRefreshToken(string token)
    {
        // Refresh tokens are opaque; validation done via DB lookup in handler
        return null;
    }
}
