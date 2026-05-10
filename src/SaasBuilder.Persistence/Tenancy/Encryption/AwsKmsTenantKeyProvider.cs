using System;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Tenancy.Encryption;

namespace SaasBuilder.Persistence.Tenancy.Encryption;

/// <summary>
/// Stub <see cref="ITenantKeyProvider"/> backed by AWS Key Management Service (KMS).
/// Each tenant's Key Encryption Key (KEK) is an AWS KMS Customer Managed Key (CMK).
/// </summary>
/// <remarks>
/// TODO(Phase 3.4): Implement AWS KMS adapter.
/// Required NuGet: <c>AWSSDK.KeyManagementService</c> (add to Directory.Packages.props when implementing).
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.4.
/// </remarks>
public sealed class AwsKmsTenantKeyProvider : ITenantKeyProvider
{
    /// <inheritdoc />
    public ValueTask<byte[]> GetDataKeyAsync(Guid tenantId, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.4): AwsKmsTenantKeyProvider is not yet implemented. " +
            "See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.4.");

    /// <inheritdoc />
    public ValueTask<byte[]> WrapAsync(Guid tenantId, byte[] plainKey, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.4): AwsKmsTenantKeyProvider is not yet implemented. " +
            "See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.4.");

    /// <inheritdoc />
    public ValueTask<byte[]> UnwrapAsync(Guid tenantId, byte[] wrappedKey, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.4): AwsKmsTenantKeyProvider is not yet implemented. " +
            "See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.4.");
}
