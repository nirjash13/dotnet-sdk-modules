namespace Identity.Application.Auth;

/// <summary>
/// Argon2id password hashing abstraction.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): replace PBKDF2 (current OpenIddict default) with Argon2id.
/// Upgrade path:
/// 1. Implement <see cref="IArgon2idHasher"/> using Konscious.Security.Cryptography.Argon2.
/// 2. Add a "hashVersion" column to the users table.
/// 3. On login success: check hashVersion; if &lt;2, re-hash the verified password with Argon2id in place.
/// 4. Deprecate PBKDF2 after a rolling migration window.
/// Do NOT swap the hasher today — this is a breaking change requiring a migration plan.
/// </remarks>
public interface IArgon2idHasher
{
    /// <summary>Hashes the given password using Argon2id.</summary>
    string Hash(string password);

    /// <summary>Verifies the password against the stored hash.</summary>
    bool Verify(string password, string hash);
}
