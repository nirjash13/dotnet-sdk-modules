using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Abstractions;
using Ledger.Application.Commands;
using Ledger.Application.Queries;
using Ledger.Contracts;
using Ledger.Infrastructure.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Api;

/// <summary>
/// <see cref="IModuleStartup"/> implementation for the Ledger module.
/// Discovered by <c>ReflectionModuleLoader</c> at startup via assembly scan.
/// </summary>
public sealed class LedgerModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Infrastructure: EF Core DbContext, Marten, repositories, unit of work, and validators.
        services.AddLedgerInfrastructure(config);

        // MassTransit consumers: register command + query handlers so the Mediator
        // can discover and route messages to them.
        services.AddMediator(cfg =>
        {
            cfg.AddConsumer<PostTransactionHandler>();
            cfg.AddConsumer<GetAccountBalanceHandler>();
        });
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder ledger = endpoints
            .MapGroup("/api/v1/ledger")
            .RequireAuthorization()
            .WithTags("ledger");

        // POST /api/v1/ledger/accounts/{accountId}/transactions
        ledger.MapPost(
            "/accounts/{accountId:guid}/transactions",
            PostTransactionAsync)
            .WithName("Ledger_PostTransaction")
            .WithSummary("Posts a monetary transaction to the specified account.");

        // GET /api/v1/ledger/accounts/{accountId}/balance
        ledger.MapGet(
            "/accounts/{accountId:guid}/balance",
            GetAccountBalanceAsync)
            .WithName("Ledger_GetAccountBalance")
            .WithSummary("Returns the current balance of the specified account.");
    }

    private static async Task<IResult> PostTransactionAsync(
        Guid accountId,
        PostTransactionRequest request,
        IRequestClient<PostTransactionCommand> client,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(CreateProblem("Request body is required."));
        }

        PostTransactionCommand command = new PostTransactionCommand(
            accountId: accountId,
            amount: request.Amount,
            currency: request.Currency ?? string.Empty,
            memo: request.Memo,
            idempotencyKey: request.IdempotencyKey);

        Response<Result<Guid>> response = await client
            .GetResponse<Result<Guid>>(command, ct)
            .ConfigureAwait(false);

        Result<Guid> result = response.Message;

        if (!result.IsSuccess)
        {
            // A not-found failure is returned as 404 (IDOR-safe — not 403).
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Results.NotFound(CreateProblem(result.Error));
            }

            return Results.BadRequest(CreateProblem(result.Error ?? "Command failed."));
        }

        // Return 201 Created with the posting Id.
        return Results.Created(
            $"/api/v1/ledger/accounts/{accountId}/transactions/{result.Value}",
            new { TransactionId = result.Value });
    }

    private static async Task<IResult> GetAccountBalanceAsync(
        Guid accountId,
        IRequestClient<GetAccountBalanceQuery> client,
        CancellationToken ct)
    {
        GetAccountBalanceQuery query = new GetAccountBalanceQuery(accountId);

        Response<Result<AccountBalanceDto>> response = await client
            .GetResponse<Result<AccountBalanceDto>>(query, ct)
            .ConfigureAwait(false);

        Result<AccountBalanceDto> result = response.Message;

        if (!result.IsSuccess)
        {
            // Not-found is returned as 404 — proves IDOR resistance (not 403).
            return Results.NotFound(CreateProblem(result.Error ?? "Account not found."));
        }

        return Results.Ok(result.Value);
    }

    private static object CreateProblem(string detail) => new
    {
        Type = "https://tools.ietf.org/html/rfc7807",
        Title = "Request failed.",
        Status = (int)HttpStatusCode.BadRequest,
        Detail = detail,
    };

    /// <summary>Request body for the POST transaction endpoint.</summary>
    public sealed class PostTransactionRequest
    {
        /// <summary>Gets or sets the monetary amount. Must not be zero.</summary>
        public decimal Amount { get; set; }

        /// <summary>Gets or sets the 3-character ISO 4217 currency code.</summary>
        public string? Currency { get; set; }

        /// <summary>Gets or sets the optional human-readable description.</summary>
        public string? Memo { get; set; }

        /// <summary>Gets or sets the optional client-supplied idempotency key.</summary>
        public Guid? IdempotencyKey { get; set; }
    }
}
