using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;

namespace SaasBuilder.Persistence.Tenancy.Encryption;

/// <summary>
/// EF Core <see cref="ValueConverter{TModel,TProvider}"/> that (in production) encrypts a
/// <see cref="byte"/> array using AES-256-GCM with the tenant's Data Encryption Key before
/// persisting, and decrypts on read.
/// </summary>
/// <remarks>
/// TODO(Phase 3.4): Implement actual AES-256-GCM encryption. The current implementation
/// is a pass-through stub that logs a one-time WARNING on first use and returns the value
/// unchanged. This allows development to proceed without a KMS; it MUST NOT be used in
/// production before Phase 3.4 is implemented.
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.4.
/// </remarks>
public sealed class EncryptedBytes : ValueConverter<byte[], byte[]>
{
    private static volatile bool _warnedOnce;

    /// <summary>
    /// Initializes the pass-through stub converter and logs a WARNING on first instantiation.
    /// </summary>
    /// <param name="logger">Logger for the DEV ONLY warning. May be <see langword="null"/>.</param>
    public EncryptedBytes(ILogger? logger = null)
        : base(
            value => Encrypt(value, logger),
            value => Decrypt(value, logger))
    {
    }

    private static byte[] Encrypt(byte[] value, ILogger? logger)
    {
        WarnOnce(logger);

        // TODO(Phase 3.4): Replace with AES-256-GCM encryption.
        return value;
    }

    private static byte[] Decrypt(byte[] value, ILogger? logger)
    {
        WarnOnce(logger);

        // TODO(Phase 3.4): Replace with AES-256-GCM decryption.
        return value;
    }

    private static void WarnOnce(ILogger? logger)
    {
        if (_warnedOnce)
        {
            return;
        }

        _warnedOnce = true;
        logger?.LogWarning(
            "EncryptedBytes value converter is in pass-through (DEV ONLY) mode — " +
            "values are NOT encrypted. Implement AES-256-GCM encryption in Phase 3.4.");
    }
}
