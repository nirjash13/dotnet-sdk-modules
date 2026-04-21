using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ledger.Application.Abstractions;
using Ledger.Contracts;
using Ledger.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IAccountRepository"/>.
/// </summary>
internal sealed class AccountRepository(LedgerDbContext context) : IAccountRepository
{
    /// <inheritdoc />
    public async Task<Account?> GetByIdAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        // The EF global query filter already restricts by tenantId; the explicit where is
        // defence-in-depth and makes the intent visible to code reviewers.
        // We do not eagerly load Postings here because PostTransactionHandler only adds a new
        // Posting to the Account; it does not read existing postings. EF change tracking
        // detects the new Posting entity via the Account.Postings navigation on SaveChangesAsync.
        return await context.Accounts
            .Where(a => a.Id == accountId && a.TenantId == tenantId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AccountBalanceDto?> GetBalanceAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        // Project directly at the query level — no entity materialisation.
        // AsNoTracking is implicit on projections that don't return tracked entities.
        return await context.Accounts
            .AsNoTracking()
            .Where(a => a.Id == accountId && a.TenantId == tenantId)
            .Select(a => new AccountBalanceDto(
                a.Id,
                a.Name,
                context.Postings
                    .Where(p => p.AccountId == a.Id && p.TenantId == tenantId)
                    .Sum(p => (decimal?)p.Amount.Amount) ?? 0m,
                a.Currency,
                context.Postings
                    .Count(p => p.AccountId == a.Id && p.TenantId == tenantId)))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(Account account) => context.Accounts.Add(account);
}
