using System;
using Chassis.Persistence;
using FluentValidation;
using Ledger.Application.Abstractions;
using Ledger.Application.Commands;
using Ledger.Application.Queries;
using Ledger.Application.Validators;
using Ledger.Infrastructure.Events;
using Ledger.Infrastructure.Persistence;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Infrastructure.Extensions;

/// <summary>
/// Extension methods that wire up the Ledger module's infrastructure services:
/// EF Core <see cref="LedgerDbContext"/>, Marten audit event store, and repositories.
/// </summary>
public static class LedgerInfrastructureExtensions
{
    /// <summary>
    /// Registers all infrastructure services required by the Ledger module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddLedgerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Chassis")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'Chassis' (or fallback 'DefaultConnection') is not configured. " +
                "Set it via environment variable 'ConnectionStrings__Chassis'.");

        // 1. EF Core DbContext via Chassis persistence helper (adds interceptor + accessor).
        services.AddChassisPersistence<LedgerDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(LedgerDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "ledger");
                });
        });

        // 2. Marten document store for audit events.
        // TenancyStyle.Conjoined: all tenants share a single table with a tenant_id column.
        // The schema is "ledger_events" (separate from EF Core's "ledger" schema).
        // Connection string is shared with EF Core — same Postgres instance, different schema.
        services.AddMarten(opts =>
        {
            opts.Connection(connectionString);
            opts.DatabaseSchemaName = "ledger_events";

            // Auto-create tables in development; in production apply migrations explicitly.
            // JasperFx.AutoCreate.All creates tables + schema if missing (Marten 8.x / JasperFx).
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;

            // Multi-tenant: store DomainAuditEvent with tenant_id discriminator column.
            opts.Policies.AllDocumentsAreMultiTenanted();
        })
        .UseLightweightSessions();

        // 3. Application-layer services.
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ILedgerUnitOfWork, LedgerUnitOfWork>();
        services.AddScoped<IDomainAuditEventStore, MartenDomainAuditEventStore>();

        // Direct in-process execution services for the HTTP hot path — bypass MT
        // request/response so write latency is not coupled to bus round-trips or
        // downstream projection consumer speed.
        services.AddScoped<IPostTransactionService, PostTransactionService>();
        services.AddScoped<IGetAccountBalanceService, GetAccountBalanceService>();

        // 4. FluentValidation — register all validators from the Application assembly.
        services.AddValidatorsFromAssemblyContaining<PostTransactionCommandValidator>();

        return services;
    }
}
