using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Audit.Infrastructure.Entities;
using Audit.Infrastructure.Loggers;
using FluentAssertions;
using Xunit;

namespace SaasBuilder.Modules.Phase5Tests;

/// <summary>
/// Load-bearing tests for the Audit module.
/// Test budget: ≤2 tests (hard budget per testing.md).
/// </summary>
public sealed class AuditLoggerTests
{
    /// <summary>
    /// Hash chain: given a sequence of 3 entries where each PrevHash is derived from the previous Hash,
    /// VerifyChain returns true. Tampering with the middle entry breaks the chain.
    /// </summary>
    [Fact]
    public void HashChainVerifier_WhenMiddleEntryTampered_ChainVerificationFails()
    {
        // Arrange — build a chain of 3 entries with valid hashes.
        Guid tenantId = new Guid("AAAAAAAA-0000-0000-0000-000000000001");

        AuditEntry entry1 = BuildEntry(tenantId, null);
        AuditEntry entry2 = BuildEntry(tenantId, entry1.Hash);
        AuditEntry entry3 = BuildEntry(tenantId, entry2.Hash);

        List<AuditEntry> chain = new List<AuditEntry> { entry1, entry2, entry3 };

        // Sanity: valid chain should pass.
        HashChainedAuditLogger.VerifyChain(chain).Should().BeTrue("unmodified chain must verify");

        // Tamper: swap entry2's hash to break the link to entry3.
        AuditEntry tampered = BuildEntryWithCustomHash(tenantId, entry1.Hash, hash: "deadbeef");
        List<AuditEntry> tamperedChain = new List<AuditEntry> { entry1, tampered, entry3 };

        // Act + Assert
        HashChainedAuditLogger.VerifyChain(tamperedChain).Should().BeFalse("tampered chain must fail verification");
    }

    // ---------------------------------------------------------------------------
    // Helpers — build AuditEntry instances with computed hashes for chain testing.
    // ---------------------------------------------------------------------------

    private static AuditEntry BuildEntry(Guid tenantId, string? prevHash)
    {
        Guid id = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        // Compute canonical hash using the same algorithm as HashChainedAuditLogger.
        object canonical = new
        {
            tenantId,
            actorId = "test-actor",
            action = "resource.created",
            resourceType = "Invoice",
            resourceId = id.ToString(),
            timestamp = timestamp.UtcTicks,
        };

        string canonicalJson = JsonSerializer.Serialize(canonical, new JsonSerializerOptions { WriteIndented = false });
        string hash = ComputeHash((prevHash ?? string.Empty) + canonicalJson);

        return new AuditEntry(
            id: id,
            tenantId: tenantId,
            actorId: "test-actor",
            action: "resource.created",
            resourceType: "Invoice",
            resourceId: id.ToString(),
            beforeJson: null,
            afterJson: null,
            ipAddress: null,
            userAgent: null,
            correlationId: null,
            timestamp: timestamp,
            prevHash: prevHash,
            hash: hash);
    }

    private static AuditEntry BuildEntryWithCustomHash(Guid tenantId, string? prevHash, string hash)
    {
        return new AuditEntry(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            actorId: "test-actor",
            action: "resource.created",
            resourceType: "Invoice",
            resourceId: "tampered",
            beforeJson: null,
            afterJson: null,
            ipAddress: null,
            userAgent: null,
            correlationId: null,
            timestamp: DateTimeOffset.UtcNow,
            prevHash: prevHash,
            hash: hash);
    }

    private static string ComputeHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
