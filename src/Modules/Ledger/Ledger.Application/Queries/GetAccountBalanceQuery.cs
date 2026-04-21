using System;
using Chassis.SharedKernel.Abstractions;
using Ledger.Contracts;

namespace Ledger.Application.Queries;

/// <summary>
/// Query to retrieve the current balance of a ledger account.
/// Handled by <see cref="GetAccountBalanceHandler"/>.
/// </summary>
public sealed class GetAccountBalanceQuery : IQuery<Result<AccountBalanceDto>>
{
    /// <summary>Initializes the query with required fields.</summary>
    public GetAccountBalanceQuery(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));
        }

        AccountId = accountId;
    }

    /// <summary>Gets the account to retrieve the balance for.</summary>
    public Guid AccountId { get; }
}
