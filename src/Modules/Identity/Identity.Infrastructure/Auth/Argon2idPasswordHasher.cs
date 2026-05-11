using System;
using System.Security.Cryptography;
using Identity.Application.Auth;
using Konscious.Security.Cryptography;

namespace Identity.Infrastructure.Auth;

/// <summary>
/// Argon2id password hasher. Uses Argon2id with OWASP-recommended parameters:
/// m=12288 KiB (12 MB), t=3 iterations, p=1 thread, 256-bit output.
/// Format stored: "v1$base64(salt)$base64(hash)" — 64 bytes for each.
/// </summary>
public sealed class Argon2idPasswordHasher : IArgon2idHasher
{
    // OWASP ASVS 5.0 §2.4.1 minimum — Argon2id m≥12 MiB, t≥3, p≥1.
    private const int MemorySize = 12288; // KiB
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 1;
    private const int SaltBytes = 32;
    private const int HashBytes = 32;
    private const string FormatVersion = "v1";

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = ComputeHash(password, salt);

        return $"{FormatVersion}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <inheritdoc />
    public bool Verify(string password, string hash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(hash);

        string[] parts = hash.Split('$');

        // Expected: "v1$<salt>$<hash>"
        if (parts.Length != 3 || parts[0] != FormatVersion)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actualHash = ComputeHash(password, salt);

        // Constant-time compare to prevent timing attacks.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        // Argon2id combines Argon2d (GPU-resistance) and Argon2i (side-channel resistance).
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemorySize,
            Iterations = Iterations,
            DegreeOfParallelism = DegreeOfParallelism,
        };

        return argon2.GetBytes(HashBytes);
    }
}
