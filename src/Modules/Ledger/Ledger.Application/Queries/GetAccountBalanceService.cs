using System;
using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;
using Chassis.SharedKernel.Tenancy;
using Ledger.Application.Abstractions;
using Ledger.Contracts;
using Microsoft.Extensions.Logging;

namespace Ledger.Application.Queries;

/// <summary>
/// Implements <see cref="IGetAccountBalanceService"/> — the in-process execution path
/// for the balance query. Called directly by the HTTP endpoint so read latency is
/// bounded by the Postgres projection query alone.
/// </summary>
public sealed class GetAccountBalanceService : IGetAccountBalanceService
{
    private readonly IAccountRepository _accounts;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ILogger<GetAccountBalanceService> _logger;

    /// <summary>Initializes the service with its dependencies.</summary>
    public GetAccountBalanceService(
        IAccountRepository accounts,
        ITenantContextAccessor tenantAccessor,
        ILogger<GetAccountBalanceService> logger)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<AccountBalanceDto>> ExecuteAsync(GetAccountBalanceQuery query, CancellationToken ct = default)
    {
        if (query is null)
        {
            return Result<AccountBalanceDto>.Failure("Query is null.");
        }

        ITenantContext? tenantContext = _tenantAccessor.Current;
        if (tenantContext is null)
        {
            return Result<AccountBalanceDto>.Failure("No tenant context is active.");
        }

        Guid tenantId = tenantContext.TenantId;

        AccountBalanceDto? dto = await _accounts
            .GetBalanceAsync(query.AccountId, tenantId, ct)
            .ConfigureAwait(false);

        if (dto is null)
        {
            _logger.LogDebug(
                "Account {AccountId} not found for tenant {TenantId}.",
                query.AccountId,
                tenantId);

            return Result<AccountBalanceDto>.Failure($"Account '{query.AccountId}' not found.");
        }

        return Result<AccountBalanceDto>.Success(dto);
    }
}
