using System.Security.Cryptography;
using System.Text;
using DeployFlow.Application.Common;
using Microsoft.Extensions.Configuration;

namespace DeployFlow.Infrastructure.Identity;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionService(IConfiguration config)
    {
        var secret = config["Encryption:Secret"]
            ?? throw new InvalidOperationException("Encryption:Secret not configured.");
        // Derive a 256-bit key from the secret
        using var deriveBytes = new Rfc2898DeriveBytes(
            secret, Encoding.UTF8.GetBytes("deployflow-salt"), 100_000, HashAlgorithmName.SHA256);
        _key = deriveBytes.GetBytes(32);
        _iv = deriveBytes.GetBytes(16);
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    public string Decrypt(string cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherText);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool VerifyPassword(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}
