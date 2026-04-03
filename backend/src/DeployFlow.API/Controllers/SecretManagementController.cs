using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace DeployFlow.API.Controllers;

/// <summary>Vault-like encrypted secret management.</summary>
[Authorize]
[Route("api/secrets")]
public class SecretManagementController : BaseController
{
    private readonly ApplicationDbContext _db;
    // In production this key comes from Azure Key Vault / environment variable
    private static readonly byte[] _aesKey = DeriveKey(
        Environment.GetEnvironmentVariable("DEPLOYFLOW_SECRET_KEY") ?? "deployflow-default-dev-key-change-me");

    public SecretManagementController(IMediator mediator, ApplicationDbContext db) : base(mediator) => _db = db;

    // ── Vaults ────────────────────────────────────────────────────────────────

    [HttpGet("vaults")]
    public async Task<IActionResult> GetVaults(CancellationToken ct)
    {
        var vaults = await _db.SecretVaults.Where(v => !v.IsDeleted).ToListAsync(ct);
        return Ok(vaults);
    }

    [HttpPost("vaults")]
    public async Task<IActionResult> CreateVault([FromBody] CreateVaultRequest req, CancellationToken ct)
    {
        var vault = new SecretVault { TenantId = req.TenantId, Name = req.Name, Description = req.Description };
        _db.SecretVaults.Add(vault);
        await _db.SaveChangesAsync(ct);
        return Ok(vault);
    }

    [HttpDelete("vaults/{id:guid}")]
    public async Task<IActionResult> DeleteVault(Guid id, CancellationToken ct)
    {
        var vault = await _db.SecretVaults.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vault is null) return NotFound();
        vault.SoftDelete();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Secrets ───────────────────────────────────────────────────────────────

    [HttpGet("vaults/{vaultId:guid}/secrets")]
    public async Task<IActionResult> ListSecrets(Guid vaultId, CancellationToken ct)
    {
        var secrets = await _db.VaultSecrets
            .Where(s => s.VaultId == vaultId && !s.IsDeleted)
            .Select(s => new
            {
                s.Id, s.Key, s.Description, s.SecretType, s.RotationPolicy,
                s.ExpiresAt, s.Version, s.Tags, s.CreatedAt, s.UpdatedAt,
                IsExpired = s.ExpiresAt.HasValue && DateTime.UtcNow > s.ExpiresAt.Value,
                // Never return encrypted value in list view
            })
            .ToListAsync(ct);
        return Ok(secrets);
    }

    [HttpPost("vaults/{vaultId:guid}/secrets")]
    public async Task<IActionResult> CreateSecret(Guid vaultId, [FromBody] CreateSecretRequest req, CancellationToken ct)
    {
        var vault = await _db.SecretVaults.FirstOrDefaultAsync(v => v.Id == vaultId && !v.IsDeleted, ct);
        if (vault is null) return NotFound(new { error = "Vault not found." });
        if (vault.IsLocked) return BadRequest(new { error = "Vault is locked. Unlock it before writing secrets." });

        var encrypted = Encrypt(req.Value);
        var secret = new VaultSecret
        {
            TenantId = req.TenantId,
            VaultId = vaultId,
            Key = req.Key,
            EncryptedValue = encrypted,
            Description = req.Description,
            SecretType = req.SecretType,
            RotationPolicy = req.RotationPolicy,
            ExpiresAt = req.ExpiresAt,
            Tags = req.Tags,
        };
        _db.VaultSecrets.Add(secret);
        vault.SecretCount++;
        await _db.SaveChangesAsync(ct);
        AuditSecret(secret.Id, secret.TenantId, null, "write", HttpContext.Connection.RemoteIpAddress?.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok(new { secret.Id, secret.Key, secret.Version, secret.CreatedAt });
    }

    /// <summary>Reveal a secret value (decrypts on the fly). Logged to audit trail.</summary>
    [HttpGet("vaults/{vaultId:guid}/secrets/{id:guid}/reveal")]
    public async Task<IActionResult> RevealSecret(Guid vaultId, Guid id, CancellationToken ct)
    {
        var secret = await _db.VaultSecrets.FirstOrDefaultAsync(s => s.Id == id && s.VaultId == vaultId && !s.IsDeleted, ct);
        if (secret is null) return NotFound();
        var plain = Decrypt(secret.EncryptedValue);
        AuditSecret(id, secret.TenantId, null, "read", HttpContext.Connection.RemoteIpAddress?.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok(new { value = plain });
    }

    /// <summary>Update (rotate) a secret value — increments version.</summary>
    [HttpPut("vaults/{vaultId:guid}/secrets/{id:guid}")]
    public async Task<IActionResult> UpdateSecret(Guid vaultId, Guid id, [FromBody] UpdateSecretRequest req, CancellationToken ct)
    {
        var secret = await _db.VaultSecrets.FirstOrDefaultAsync(s => s.Id == id && s.VaultId == vaultId, ct);
        if (secret is null) return NotFound();
        secret.EncryptedValue = Encrypt(req.Value);
        secret.Version++;
        secret.Description = req.Description ?? secret.Description;
        secret.ExpiresAt = req.ExpiresAt ?? secret.ExpiresAt;
        secret.Touch();
        AuditSecret(id, secret.TenantId, null, "rotate", HttpContext.Connection.RemoteIpAddress?.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok(new { secret.Id, secret.Key, secret.Version });
    }

    [HttpDelete("vaults/{vaultId:guid}/secrets/{id:guid}")]
    public async Task<IActionResult> DeleteSecret(Guid vaultId, Guid id, CancellationToken ct)
    {
        var secret = await _db.VaultSecrets.FirstOrDefaultAsync(s => s.Id == id && s.VaultId == vaultId, ct);
        if (secret is null) return NotFound();
        secret.SoftDelete();
        AuditSecret(id, secret.TenantId, null, "delete", HttpContext.Connection.RemoteIpAddress?.ToString());
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLog([FromQuery] Guid? secretId, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var q = _db.SecretAuditEntries.AsQueryable();
        if (secretId.HasValue) q = q.Where(a => a.SecretId == secretId);
        return Ok(await q.OrderByDescending(a => a.CreatedAt).Take(limit).ToListAsync(ct));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AuditSecret(Guid secretId, Guid tenantId, Guid? userId, string action, string? ip)
    {
        _db.SecretAuditEntries.Add(new SecretAuditEntry
        {
            TenantId = tenantId,
            SecretId = secretId,
            UserId = userId,
            Action = action,
            IpAddress = ip,
            Success = true,
        });
    }

    private static string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.GenerateIV();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
            sw.Write(plainText);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string Decrypt(string cipherBase64)
    {
        var buf = Convert.FromBase64String(cipherBase64);
        using var aes = Aes.Create();
        aes.Key = _aesKey;
        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(buf, 0, iv, 0, iv.Length);
        aes.IV = iv;
        using var ms = new MemoryStream(buf, iv.Length, buf.Length - iv.Length);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }

    private static byte[] DeriveKey(string input)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(input));
    }
}

public record CreateVaultRequest(Guid TenantId, string Name, string? Description);
public record CreateSecretRequest(
    Guid TenantId, string Key, string Value, string? Description,
    SecretType SecretType, SecretRotationPolicy RotationPolicy,
    DateTime? ExpiresAt, string? Tags
);
public record UpdateSecretRequest(string Value, string? Description, DateTime? ExpiresAt);
