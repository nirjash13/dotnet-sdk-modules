using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Entities;

/// <summary>
/// TOTP (Time-based One-Time Password) credential for a user.
/// Stores the encrypted TOTP secret and hashed recovery codes.
/// </summary>
public sealed class TotpCredential
{
    private List<string> _hashedRecoveryCodes = new List<string>();

    private TotpCredential()
    {
    }

    /// <summary>Gets the credential's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the owning user's identifier.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the base32-encoded TOTP secret (encrypted at rest via column-level encryption).</summary>
    public string EncryptedSecret { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether this TOTP credential has been confirmed/verified by the user.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>Gets the UTC time the credential was confirmed.</summary>
    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>
    /// Gets or sets the JSON-serialized hashed recovery codes.
    /// EF Core maps this directly; domain code mutates via <see cref="ConsumeRecoveryCode"/>.
    /// </summary>
    public string HashedRecoveryCodesJson { get; private set; } = "[]";

    /// <summary>
    /// Gets the hashed recovery codes (deserialized view — not mapped by EF Core).
    /// </summary>
    public IReadOnlyList<string> HashedRecoveryCodes => _hashedRecoveryCodes;

    /// <summary>Creates a new (unconfirmed) <see cref="TotpCredential"/>.</summary>
    public static TotpCredential Create(
        Guid id,
        Guid userId,
        string encryptedSecret,
        IEnumerable<string> hashedRecoveryCodes)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("TotpCredential id must not be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("UserId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            throw new IdentityDomainException("Encrypted secret must not be empty.");
        }

        List<string> codes = hashedRecoveryCodes.ToList();
        if (codes.Count == 0)
        {
            throw new IdentityDomainException("At least one recovery code is required.");
        }

        var credential = new TotpCredential
        {
            Id = id,
            UserId = userId,
            EncryptedSecret = encryptedSecret,
        };

        credential._hashedRecoveryCodes.AddRange(codes);
        credential.HashedRecoveryCodesJson = JsonSerializer.Serialize(codes);
        return credential;
    }

    /// <summary>Marks this credential as confirmed after the user successfully verifies a TOTP code.</summary>
    public void Confirm()
    {
        if (IsConfirmed)
        {
            throw new IdentityDomainException("TOTP credential is already confirmed.");
        }

        IsConfirmed = true;
        ConfirmedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Attempts to consume a recovery code. Returns <see langword="true"/> if the code was found and consumed.
    /// The matched hash is removed from the list (single-use).
    /// </summary>
    public bool ConsumeRecoveryCode(string hashedCode)
    {
        int index = _hashedRecoveryCodes.IndexOf(hashedCode);
        if (index < 0)
        {
            return false;
        }

        _hashedRecoveryCodes.RemoveAt(index);
        HashedRecoveryCodesJson = JsonSerializer.Serialize(_hashedRecoveryCodes);
        return true;
    }

    /// <summary>
    /// Hydrates the in-memory list from <see cref="HashedRecoveryCodesJson"/>.
    /// Called after EF Core materializes the entity from the database.
    /// </summary>
    internal void HydrateFromJson()
    {
        _hashedRecoveryCodes = JsonSerializer.Deserialize<List<string>>(HashedRecoveryCodesJson)
            ?? new List<string>();
    }
}
