using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SaasBuilder.Persistence.Tenancy.Encryption;

/// <summary>
/// EF Core <see cref="ValueConverter{TModel,TProvider}"/> that encrypts a <see cref="string"/>
/// using AES-256-GCM with a fixed per-tenant Data Encryption Key before persisting, and decrypts
/// on read.
/// </summary>
/// <remarks>
/// <para>
/// Storage format (Base64-encoded concatenation):
/// <c>nonce(12 bytes) || ciphertext(N bytes) || tag(16 bytes)</c>
/// </para>
/// <para>
/// The <paramref name="getKey"/> delegate is called on every encrypt/decrypt operation.
/// For per-tenant encryption the caller should resolve the DEK from <see cref="ITenantKeyProvider"/>
/// and pass it as a closure — typically done inside the EF Core model convention that wires
/// this converter onto <c>[Encrypted]</c> properties.
/// </para>
/// <para>
/// <strong>NOTE:</strong> EF Core value converters receive expressions that are compiled at
/// model-build time. Because <see cref="System.Security.Cryptography.AesGcm"/> is not
/// expression-safe, the static helper methods are called through ordinary delegates
/// (not expression trees), which is correct for server-side (save/load) operations only —
/// client-side LINQ translation is not supported for encrypted columns.
/// </para>
/// </remarks>
public sealed class AesGcmEncryptedStringConverter : ValueConverter<string, string>
{
    private const int NonceSizeBytes = 12; // 96-bit nonce for GCM.
    private const int TagSizeBytes = 16; // 128-bit authentication tag.

    /// <summary>
    /// Initializes the converter with a DEK source delegate.
    /// </summary>
    /// <param name="getKey">
    /// Returns the 32-byte AES-256 Data Encryption Key for the current tenant.
    /// Called on every encrypt/decrypt; implementors SHOULD cache this value.
    /// </param>
    public AesGcmEncryptedStringConverter(Func<byte[]> getKey)
        : base(
            plaintext => Encrypt(plaintext, getKey()),
            ciphertext => Decrypt(ciphertext, getKey()))
    {
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with AES-256-GCM.
    /// Returns a Base64 string: nonce || ciphertext || tag.
    /// </summary>
    public static string Encrypt(string plaintext, byte[] key)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[TagSizeBytes];

        using AesGcm aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Combine nonce || ciphertext || tag into a single byte array, then Base64-encode.
        byte[] combined = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, combined, NonceSizeBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, NonceSizeBytes + ciphertext.Length, TagSizeBytes);

        return Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Decrypts a Base64-encoded AES-256-GCM ciphertext (nonce || ciphertext || tag).
    /// Returns the original plaintext string.
    /// </summary>
    public static string Decrypt(string encoded, byte[] key)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return encoded;
        }

        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            // Value was not Base64 — it may be a legacy plaintext value stored before encryption
            // was enabled. Return as-is and log a warning in a future phase.
            throw new CryptographicException(
                "Failed to Base64-decode encrypted column value. " +
                "If this column was added after encryption was enabled, the stored value is corrupt. " +
                "If migrating existing data, run the re-encryption migration first.",
                ex);
        }

        int ciphertextLength = combined.Length - NonceSizeBytes - TagSizeBytes;
        if (ciphertextLength < 0)
        {
            throw new CryptographicException(
                $"Encrypted value is too short ({combined.Length} bytes). " +
                "Minimum is nonce(12) + tag(16) = 28 bytes.");
        }

        byte[] nonce = combined[..NonceSizeBytes];
        int ciphertextEnd = NonceSizeBytes + ciphertextLength;
        byte[] ciphertext = combined[NonceSizeBytes..ciphertextEnd];
        byte[] tag = combined[ciphertextEnd..];
        byte[] plaintext = new byte[ciphertextLength];

        using AesGcm aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
