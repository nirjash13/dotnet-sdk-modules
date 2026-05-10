using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy.Encryption;

namespace SaasBuilder.Persistence.Tenancy.Encryption;

/// <summary>
/// DEV ONLY — <see cref="ITenantKeyProvider"/> that reads 256-bit encryption keys from a
/// local directory. Each tenant key is stored as a 32-byte file named <c>{tenantId}.key</c>.
/// </summary>
/// <remarks>
/// <strong>WARNING:</strong> This implementation is for local development only.
/// Keys are stored on the filesystem unencrypted. Do NOT use in staging or production.
/// Use <c>AzureKeyVaultTenantKeyProvider</c>, <c>AwsKmsTenantKeyProvider</c>, or
/// <c>GoogleKmsTenantKeyProvider</c> in non-development environments.
///
/// The key directory defaults to <c>%APPDATA%/saasbuilder/dev-keys/</c> on Windows and
/// <c>~/.saasbuilder/dev-keys/</c> on Linux/macOS. Override via <c>Tenancy:DevKeyDirectory</c>
/// in configuration.
/// </remarks>
public sealed class FileSystemTenantKeyProvider : ITenantKeyProvider
{
    private const int KeySizeBytes = 32; // 256-bit key for AES-256.
    private const string ConfigKey = "Tenancy:DevKeyDirectory";

    private readonly string _keyDirectory;
    private readonly ILogger<FileSystemTenantKeyProvider> _logger;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger for the DEV ONLY warning.</param>
    public FileSystemTenantKeyProvider(
        IConfiguration configuration,
        ILogger<FileSystemTenantKeyProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        string defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "saasbuilder",
            "dev-keys");

        _keyDirectory = configuration[ConfigKey] ?? defaultDir;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Creates a new random key if the key file does not exist yet.
    /// The key is cached in the file for subsequent calls.
    /// </remarks>
    public async ValueTask<byte[]> GetDataKeyAsync(Guid tenantId, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "DEV ONLY — FileSystemTenantKeyProvider is reading a plaintext key for tenant {TenantId}. " +
            "This provider must not be used outside of local development.",
            tenantId);

        string keyPath = Path.Combine(_keyDirectory, $"{tenantId:N}.key");

        if (File.Exists(keyPath))
        {
            byte[] existing = await File.ReadAllBytesAsync(keyPath, ct).ConfigureAwait(false);
            if (existing.Length == KeySizeBytes)
            {
                return existing;
            }
        }

        // Generate and persist a new random key.
        Directory.CreateDirectory(_keyDirectory);
        byte[] newKey = RandomNumberGenerator.GetBytes(KeySizeBytes);
        await File.WriteAllBytesAsync(keyPath, newKey, ct).ConfigureAwait(false);
        return newKey;
    }

    /// <inheritdoc />
    /// <remarks>DEV ONLY — returns the key unchanged (no wrapping).</remarks>
    public ValueTask<byte[]> WrapAsync(Guid tenantId, byte[] plainKey, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "DEV ONLY — FileSystemTenantKeyProvider.WrapAsync returns the key unchanged. " +
            "This is NOT secure. Use a KMS adapter in production.");
        return new ValueTask<byte[]>(plainKey);
    }

    /// <inheritdoc />
    /// <remarks>DEV ONLY — returns the wrapped key unchanged (no unwrapping).</remarks>
    public ValueTask<byte[]> UnwrapAsync(Guid tenantId, byte[] wrappedKey, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "DEV ONLY — FileSystemTenantKeyProvider.UnwrapAsync returns the key unchanged. " +
            "This is NOT secure. Use a KMS adapter in production.");
        return new ValueTask<byte[]>(wrappedKey);
    }
}
