using System;

namespace Ledger.Contracts;

/// <summary>
/// Response DTO for an account balance query.
/// </summary>
public sealed class AccountBalanceDto
{
    /// <summary>Initializes all required fields.</summary>
    public AccountBalanceDto(
        Guid accountId,
        string accountName,
        decimal balance,
        string currency,
        int postingCount)
    {
        AccountId = accountId;
        AccountName = accountName ?? throw new ArgumentNullException(nameof(accountName));
        Balance = balance;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        PostingCount = postingCount;
    }

    /// <summary>Gets the account identifier.</summary>
    public Guid AccountId { get; }

    /// <summary>Gets the account display name.</summary>
    public string AccountName { get; }

    /// <summary>Gets the current balance (sum of all postings).</summary>
    public decimal Balance { get; }

    /// <summary>Gets the 3-character ISO 4217 currency code for this account.</summary>
    public string Currency { get; }

    /// <summary>Gets the total number of postings on this account.</summary>
    public int PostingCount { get; }
}
