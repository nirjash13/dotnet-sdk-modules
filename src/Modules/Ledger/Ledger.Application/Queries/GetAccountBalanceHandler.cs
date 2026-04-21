using System;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;
using Chassis.SharedKernel.Tenancy;
using Ledger.Application.Abstractions;
using Ledger.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ledger.Application.Queries;

/// <summary>
/// Handles <see cref="GetAccountBalanceQuery"/> by projecting the account balance
/// directly at the query level (no entity loading, no N+1).
/// </summary>
public sealed class GetAccountBalanceHandler : IConsumer<GetAccountBalanceQuery>
{
    private readonly IAccountRepository _accounts;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ILogger<GetAccountBalanceHandler> _logger;

    /// <summary>Initializes the handler with its dependencies.</summary>
    public GetAccountBalanceHandler(
        IAccountRepository accounts,
        ITenantContextAccessor tenantAccessor,
        ILogger<GetAccountBalanceHandler> logger)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<GetAccountBalanceQuery> context)
    {
        GetAccountBalanceQuery query = context.Message;
        ITenantContext? tenantContext = _tenantAccessor.Current;

        if (tenantContext is null)
        {
            await context.RespondAsync(Result<AccountBalanceDto>.Failure("No tenant context is active."))
                .ConfigureAwait(false);
            return;
        }

        Guid tenantId = tenantContext.TenantId;

        // Repository projects directly to DTO — no entity loading, AsNoTracking enforced in impl.
        AccountBalanceDto? dto = await _accounts
            .GetBalanceAsync(query.AccountId, tenantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            _logger.LogDebug(
                "Account {AccountId} not found for tenant {TenantId}.",
                query.AccountId,
                tenantId);

            await context.RespondAsync(Result<AccountBalanceDto>.Failure(
                $"Account '{query.AccountId}' not found."))
                .ConfigureAwait(false);
            return;
        }

        await context.RespondAsync(Result<AccountBalanceDto>.Success(dto))
            .ConfigureAwait(false);
    }
}
