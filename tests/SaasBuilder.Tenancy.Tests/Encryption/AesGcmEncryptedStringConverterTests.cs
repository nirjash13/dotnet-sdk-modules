using System;
using System.Security.Cryptography;
using FluentAssertions;
using SaasBuilder.Persistence.Tenancy.Encryption;
using Xunit;

namespace SaasBuilder.Tenancy.Tests.Encryption;

/// <summary>
/// Load-bearing tests for AES-256-GCM envelope encryption.
/// These tests verify the encrypt/decrypt contract that protects column-level sensitive data.
/// </summary>
public sealed class AesGcmEncryptedStringConverterTests
{
    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    // ── Test 1: round-trip — encrypt then decrypt returns original plaintext ─────
    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
    {
        byte[] key = NewKey();
        const string plaintext = "sensitive-value-abc123";

        string ciphertext = AesGcmEncryptedStringConverter.Encrypt(plaintext, key);
        string roundTripped = AesGcmEncryptedStringConverter.Decrypt(ciphertext, key);

        roundTripped.Should().Be(plaintext);
    }

    // ── Test 2: empty string is returned unchanged (not encrypted) ───────────────
    [Fact]
    public void Encrypt_WhenPlaintextIsEmpty_ReturnsEmptyWithoutEncryption()
    {
        byte[] key = NewKey();

        string result = AesGcmEncryptedStringConverter.Encrypt(string.Empty, key);

        result.Should().BeEmpty(because: "empty strings are stored as-is to simplify nullable handling");
    }

    // ── Test 3: wrong key on decrypt produces CryptographicException ─────────────
    [Fact]
    public void Decrypt_WhenKeyIsWrong_ThrowsCryptographicException()
    {
        byte[] encryptKey = NewKey();
        byte[] wrongKey = NewKey();
        const string plaintext = "secret";

        string ciphertext = AesGcmEncryptedStringConverter.Encrypt(plaintext, encryptKey);

        Action act = () => AesGcmEncryptedStringConverter.Decrypt(ciphertext, wrongKey);

        act.Should().Throw<CryptographicException>(
            because: "AES-GCM authentication tag verification fails on key mismatch");
    }

    // ── Test 4: two encryptions of the same plaintext produce different ciphertexts ─
    // This ensures a fresh nonce is generated per encryption (semantic security).
    [Fact]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertexts()
    {
        byte[] key = NewKey();
        const string plaintext = "repeated-value";

        string first = AesGcmEncryptedStringConverter.Encrypt(plaintext, key);
        string second = AesGcmEncryptedStringConverter.Encrypt(plaintext, key);

        first.Should().NotBe(second, because: "each encryption uses a fresh random nonce");
    }
}
