namespace DeployFlow.Application.DTOs;

public record AuthUserDto(
    Guid Id,
    string Email,
    string Name,
    string Role,
    string AvatarUrl,
    Guid TenantId,
    string TenantName,
    string TenantPlan,
    bool TwoFactorEnabled,
    DateTime CreatedAt,
    bool IsActive,
    DateTime? LastLoginAt
);

public record LoginRequest(
    string Email,
    string Password,
    string? TwoFactorCode
);

public record RegisterRequest(
    string Name,
    string Email,
    string Password,
    string? TenantName
);

public record AuthTokensDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    AuthUserDto User
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ForgotPasswordRequest(
    string Email
);

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword
);

public record TwoFactorSetupDto(
    string Secret,
    string QrCodeUri,
    string[] BackupCodes
);

public record Enable2FaRequest(string Code);

public record TeamMemberDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string AvatarUrl,
    string Status,
    DateTime? JoinedAt,
    DateTime CreatedAt
);

public record InviteTeamMemberRequest(
    string Email,
    string Role
);

public record UpdateProfileRequest(
    string Name,
    string? AvatarUrl
);
