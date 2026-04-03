using DeployFlow.Domain.Common;

namespace DeployFlow.Domain.Entities;

public class TeamInvitation : TenantEntity
{
    public string Email { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Role { get; private set; } = default!;
    public string Token { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime LastSentAt { get; private set; }
    public int ResendCount { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public string InvitedByName { get; private set; } = default!;

    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;

    private TeamInvitation() { }

    public static TeamInvitation Create(
        Guid tenantId,
        string email,
        string name,
        string role,
        Guid invitedByUserId,
        string invitedByName,
        int expiryDays = 7)
        => new()
        {
            TenantId = tenantId,
            Email = email.Trim(),
            Name = name.Trim(),
            Role = role.Trim().ToLowerInvariant(),
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            LastSentAt = DateTime.UtcNow,
            ResendCount = 0,
            InvitedByUserId = invitedByUserId,
            InvitedByName = invitedByName,
        };

    public void Resend(int expiryDays = 7)
    {
        Token = Guid.NewGuid().ToString("N");
        ExpiresAt = DateTime.UtcNow.AddDays(expiryDays);
        LastSentAt = DateTime.UtcNow;
        ResendCount++;
        Touch();
    }
}