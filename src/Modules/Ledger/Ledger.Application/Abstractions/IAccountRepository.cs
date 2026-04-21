using System;
using System.Threading;
using System.Threading.Tasks;
using Ledger.Contracts;
using Ledger.Domain.Entities;

namespace Ledger.Application.Abstractions;

/// <summary>
/// Repository abstraction for <see cref="Account"/> aggregate persistence.
/// Implemented in <c>Ledger.Infrastructure</c>; consumed by Application layer handlers.
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Loads an account by its identifier and tenant, or returns <see langword="null"/> if not found.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="tenantId">The owning tenant identifier (enforced by EF global query filter).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Account?> GetByIdAsync(Guid accountId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Projects an account's balance information without loading the full posting list.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="tenantId">The owning tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AccountBalanceDto?> GetBalanceAsync(Guid accountId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Adds a new account to the repository (change tracked).</summary>
    void Add(Account account);
}
