using System;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Tenancy.Encryption;

namespace SaasBuilder.Persistence.Tenancy.Encryption;

/// <summary>
/// Stub <see cref="ITenantKeyProvider"/> backed by Google Cloud Key Management Service.
/// Each tenant's Key Encryption Key (KEK) is a Google Cloud KMS CryptoKey.
/// </summary>
/// <remarks>
/// TODO(Phase 3.4): Implement Google Cloud KMS adapter.
/// Required NuGet: <c>Google.Cloud.Kms.V1</c> (add to Directory.Packages.props when implementing).
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.4.
/// </remarks>
public sealed class GoogleKmsTenantKeyProvider : ITenantKeyProvider
{
    /// <inheritdoc />
    public ValueTask<byte[]> GetDataKeyAsync(Guid tenantId, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.4): GoogleKmsTenantKeyProvider is not yet implemented. " +
            "See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.4.");

    /// <inheritdoc />
    public ValueTask<byte[]> WrapAsync(Guid tenantId, byte[] plainKey, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.4): GoogleKmsTenantKeyProvider is not yet implemented. " +
            "See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.4.");

    /// <inheritdoc />
    public ValueTask<byte[]> UnwrapAsync(Guid tenantId, byte[] wrappedKey, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.4): GoogleKmsTenantKeyProvider is not yet implemented. " +
            "See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.4.");
}
