using System;
using System.Threading;
using System.Threading.Tasks;

namespace SaasBuilder.SharedKernel.Tenancy.Encryption;

/// <summary>
/// Provides per-tenant envelope encryption keys (Data Encryption Keys wrapped by a Key Encryption Key).
/// </summary>
/// <remarks>
/// The envelope encryption pattern:
/// <list type="number">
///   <item>A Data Encryption Key (DEK) is generated per tenant (or per record class).</item>
///   <item>The DEK is wrapped (encrypted) by a Key Encryption Key (KEK) managed in a KMS.</item>
///   <item>The wrapped DEK is stored alongside the ciphertext in the database.</item>
///   <item>At decrypt time, the KEK unwraps the DEK, which decrypts the ciphertext.</item>
/// </list>
/// Adapters: <c>FileSystemTenantKeyProvider</c> (dev only), <c>AzureKeyVaultTenantKeyProvider</c>,
/// <c>AwsKmsTenantKeyProvider</c>, <c>GoogleKmsTenantKeyProvider</c> (all TODO Phase 3.4).
/// </remarks>
public interface ITenantKeyProvider
{
    /// <summary>
    /// Returns the raw Data Encryption Key bytes for the specified tenant.
    /// The returned key is ready for use with AES-256-GCM or equivalent.
    /// </summary>
    /// <param name="tenantId">The tenant for which the DEK is requested.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The DEK bytes (32 bytes for AES-256).</returns>
    ValueTask<byte[]> GetDataKeyAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Wraps (encrypts) a DEK using the tenant's Key Encryption Key in the KMS.
    /// </summary>
    /// <param name="tenantId">The tenant whose KEK is used.</param>
    /// <param name="plainKey">The plain DEK bytes to wrap.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The wrapped (ciphertext) DEK bytes for storage.</returns>
    ValueTask<byte[]> WrapAsync(Guid tenantId, byte[] plainKey, CancellationToken ct = default);

    /// <summary>
    /// Unwraps (decrypts) a stored wrapped DEK using the tenant's Key Encryption Key in the KMS.
    /// </summary>
    /// <param name="tenantId">The tenant whose KEK is used.</param>
    /// <param name="wrappedKey">The wrapped DEK bytes retrieved from storage.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The plain DEK bytes ready for use.</returns>
    ValueTask<byte[]> UnwrapAsync(Guid tenantId, byte[] wrappedKey, CancellationToken ct = default);
}
