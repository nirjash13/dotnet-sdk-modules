using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gdpr.Application.Abstractions;
using Gdpr.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Gdpr.Infrastructure.Data;

/// <summary>EF Core implementation of <see cref="IGdprConsentRepository"/>.</summary>
internal sealed class GdprConsentRepository : IGdprConsentRepository
{
    private readonly GdprDbContext _db;

    public GdprConsentRepository(GdprDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task AppendAsync(
        Guid tenantId,
        Guid userId,
        string consentKey,
        bool granted,
        string version,
        CancellationToken ct = default)
    {
        var record = new GdprConsent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            ConsentKey = consentKey,
            Granted = granted,
            Version = version,
            Timestamp = DateTimeOffset.UtcNow,
        };

        _db.Consents.Add(record);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ConsentDto?> GetLatestAsync(
        Guid tenantId,
        Guid userId,
        string consentKey,
        CancellationToken ct = default)
    {
        return await _db.Consents
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.UserId == userId && c.ConsentKey == consentKey)
            .OrderByDescending(c => c.Timestamp)
            .Select(c => new ConsentDto(c.Id, c.UserId, c.TenantId, c.ConsentKey, c.Granted, c.Version, c.Timestamp))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
