using System;
using FeatureFlags.Infrastructure.Providers;
using FluentAssertions;
using Xunit;

namespace SaasBuilder.IntegrationTests.FeatureFlags;

/// <summary>
/// Load-bearing percentage rollout tests.
///
/// Test 1 (deterministic): Same tenantId always gets same bucket. Fails if hashing is random/time-based.
/// Test 2 (distribution): 1000 tenants on 50% rollout → 400-600 in bucket. Fails if hash is biased or modulo is wrong.
///
/// These are pure-unit tests (no DB) against the ComputeBucket algorithm, which is the
/// only implementation-specific behavior worth testing here per the Load-Bearing Filter.
/// </summary>
public sealed class PercentageRolloutTests
{
    private const string FlagKey = "new-feature-rollout";

    [Fact]
    public void SameTenantId_AlwaysGivesSameBucket()
    {
        // Arrange — deterministic hash must produce identical output for identical inputs.
        Guid tenantId = new Guid("aaaaaaaa-0001-0000-0000-000000000001");

        // Act — call multiple times.
        int bucket1 = DatabaseFeatureProvider.ComputeBucket(tenantId, FlagKey);
        int bucket2 = DatabaseFeatureProvider.ComputeBucket(tenantId, FlagKey);
        int bucket3 = DatabaseFeatureProvider.ComputeBucket(tenantId, FlagKey);

        // Assert — must be identical every call.
        bucket1.Should().Be(bucket2, because: "hash must be deterministic");
        bucket2.Should().Be(bucket3, because: "hash must be deterministic");
        bucket1.Should().BeInRange(0, 99, because: "bucket must be in range 0-99");
    }

    [Fact]
    public void FiftyPercentRollout_DistributesRoughlyEvenly()
    {
        // Arrange — 1000 unique tenant IDs; seed is fixed so the test is reproducible.
        // We use sequential GUIDs starting from a fixed base to avoid flakiness.
        const int totalTenants = 1000;
        const int targetPercentage = 50;
        int inBucketCount = 0;

        for (int i = 0; i < totalTenants; i++)
        {
            // Deterministic tenant IDs: 00000000-0000-0000-0000-{i:12x}
            Guid tenantId = new Guid(0, 0, 0, 0, 0, 0, 0, 0, (byte)(i >> 16), (byte)(i >> 8), (byte)i);
            int bucket = DatabaseFeatureProvider.ComputeBucket(tenantId, FlagKey);
            if (bucket < targetPercentage)
            {
                inBucketCount++;
            }
        }

        // Assert — with a good hash function, 50% of 1000 tenants → 400-600 in bucket.
        // This is a ±10% tolerance to avoid flakiness.
        inBucketCount.Should().BeInRange(
            minimumValue: 400,
            maximumValue: 600,
            because: $"a 50% rollout across 1000 tenants should place ~500 in-bucket (got {inBucketCount})");
    }
}
