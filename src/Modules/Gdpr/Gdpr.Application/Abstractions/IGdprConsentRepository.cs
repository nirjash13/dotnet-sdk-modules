using System;
using System.Threading;
using System.Threading.Tasks;
using Gdpr.Contracts;

namespace Gdpr.Application.Abstractions;

/// <summary>Append-only repository for consent records.</summary>
public interface IGdprConsentRepository
{
    /// <summary>Appends a new consent record. Never updates an existing row.</summary>
    Task AppendAsync(
        Guid tenantId,
        Guid userId,
        string consentKey,
        bool granted,
        string version,
        CancellationToken ct = default);

    /// <summary>Returns the most-recent consent record for the user and key, or null.</summary>
    Task<ConsentDto?> GetLatestAsync(
        Guid tenantId,
        Guid userId,
        string consentKey,
        CancellationToken ct = default);
}
