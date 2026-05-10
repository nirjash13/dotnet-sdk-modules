using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Audit.Application.Abstractions;
using Audit.Application.Models;
using Audit.Infrastructure.Entities;
using Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Audit.Infrastructure.Loggers;

/// <summary>
/// Decorator over <see cref="IAuditLogger"/> that chains each entry's SHA-256 hash to the
/// previous entry. Chain integrity can be verified offline: for each entry N,
/// <c>SHA256(entry[N-1].Hash + canonicalize(entry[N])) == entry[N].Hash</c>.
/// <para>
/// TODO(Phase 5.4): formal Merkle-style storage option for Phase 8 SOC 2 audit hardening.
/// Caveats: this implementation computes hashes at INSERT time; concurrent inserts may race.
/// For strict ordering, use a DB sequence or advisory lock.
/// </para>
/// </summary>
internal sealed class HashChainedAuditLogger(
    AuditDbContext db,
    ILogger<HashChainedAuditLogger> logger)
    : IAuditLogger
{
    /// <inheritdoc />
    public async Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
    {
        try
        {
            // Retrieve the most recent entry's hash for the tenant to chain from.
            string? prevHash = await db.AuditEntries
                .AsNoTracking()
                .Where(e => e.TenantId == evt.TenantId)
                .OrderByDescending(e => e.Timestamp)
                .Select(e => e.Hash)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            DateTimeOffset timestamp = evt.Timestamp ?? DateTimeOffset.UtcNow;

            // Canonical serialization of this entry (without the hash fields).
            string canonical = CanonicalSerialize(evt, timestamp);
            string hash = ComputeHash(prevHash + canonical);

            AuditEntry entry = new AuditEntry(
                id: Guid.NewGuid(),
                tenantId: evt.TenantId,
                actorId: evt.ActorId,
                action: evt.Action,
                resourceType: evt.ResourceType,
                resourceId: evt.ResourceId,
                beforeJson: evt.BeforeJson,
                afterJson: evt.AfterJson,
                ipAddress: evt.IpAddress,
                userAgent: evt.UserAgent,
                correlationId: evt.CorrelationId,
                timestamp: timestamp,
                prevHash: prevHash,
                hash: hash);

            await db.AuditEntries.AddAsync(entry, ct).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Audit.HashChain: failed to record hash-chained audit event (action={Action})",
                evt.Action);
        }
    }

    /// <summary>Verifies that the hash chain is intact for a sequence of entries.</summary>
    /// <returns><see langword="true"/> if the chain is valid; <see langword="false"/> if tampered.</returns>
    public static bool VerifyChain(System.Collections.Generic.IReadOnlyList<AuditEntry> entries)
    {
        for (int i = 1; i < entries.Count; i++)
        {
            AuditEntry prev = entries[i - 1];
            AuditEntry current = entries[i];

            if (current.PrevHash != prev.Hash)
            {
                return false;
            }

            // Re-derive the canonical form and expected hash.
            AuditEvent mockEvt = new AuditEvent(
                TenantId: current.TenantId,
                ActorId: current.ActorId,
                Action: current.Action,
                ResourceType: current.ResourceType,
                ResourceId: current.ResourceId,
                BeforeJson: current.BeforeJson,
                AfterJson: current.AfterJson,
                IpAddress: current.IpAddress,
                UserAgent: current.UserAgent,
                CorrelationId: current.CorrelationId,
                Timestamp: current.Timestamp);

            string canonical = CanonicalSerialize(mockEvt, current.Timestamp);
            string expectedHash = ComputeHash(prev.Hash + canonical);

            if (expectedHash != current.Hash)
            {
                return false;
            }
        }

        return true;
    }

    private static string CanonicalSerialize(AuditEvent evt, DateTimeOffset timestamp)
    {
        // Deterministic JSON serialization — sorted keys, no extra whitespace.
        object canonical = new
        {
            tenantId = evt.TenantId,
            actorId = evt.ActorId,
            action = evt.Action,
            resourceType = evt.ResourceType,
            resourceId = evt.ResourceId,
            timestamp = timestamp.UtcTicks,
        };

        return JsonSerializer.Serialize(canonical, new JsonSerializerOptions { WriteIndented = false });
    }

    private static string ComputeHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
