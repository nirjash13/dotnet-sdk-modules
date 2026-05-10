using System;
using Identity.Application.Consumers;
using Ledger.Application.Commands;
using Ledger.Application.Queries;
using Ledger.Application.Consumers;
using Ledger.Infrastructure.Persistence;
using MassTransit;
using Registration.Application.Sagas;
using Registration.Infrastructure.Persistence;
using Reporting.Application.Consumers;
using SaasBuilder.Host.Configuration;
using SaasBuilder.SharedKernel.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddSaasBuilderHost(opts =>
{
    opts.UseTransport(SaasTransport.InProc);
    opts.UseTenancy(TenantIsolation.PoolWithRls);
    opts.Observability.Enable();
    opts.RateLimiting.UsePerTenantSlidingWindow();

    // Register module consumers on the in-process mediator.
    // These are co-located here (not in the library) so SaasBuilder.Host has no
    // compile-time dependency on module assemblies and can be shipped as a NuGet package.
    opts.Transport.WithMediatorConsumers(cfg =>
    {
        // Ledger request/response handlers.
        cfg.AddConsumer<PostTransactionHandler>();
        cfg.AddConsumer<GetAccountBalanceHandler>();

        // Registration/identity/reporting consumers used by saga workflows.
        cfg.AddConsumer<CreateUserCommandConsumer>();
        cfg.AddConsumer<DeleteUserCommandConsumer>();
        cfg.AddConsumer<InitLedgerCommandConsumer>();
        cfg.AddConsumer<RollbackLedgerInitCommandConsumer>();
        cfg.AddConsumer<ProvisionReportingCommandConsumer>();
        cfg.AddConsumer<UnprovisionReportingCommandConsumer>();
        cfg.AddConsumer<LedgerTransactionPostedConsumer>();
    });

    // Register module consumers/sagas on the RabbitMQ bus.
    // Only active when Dispatch:Transport=bus in configuration.
    opts.Transport.WithBusConsumers(cfg =>
    {
        cfg.AddSagaStateMachine<RegistrationSaga, RegistrationSagaState>()
            .EntityFrameworkRepository(r =>
            {
                r.ExistingDbContext<RegistrationDbContext>();
                r.UsePostgres();
            });

        cfg.AddConsumer<CreateUserCommandConsumer>(c =>
            c.UseMessageRetry(r => r.Immediate(3)));
        cfg.AddConsumer<DeleteUserCommandConsumer>(c =>
            c.UseMessageRetry(r => r.Immediate(3)));
        cfg.AddConsumer<PostTransactionHandler>(c =>
            c.UseMessageRetry(r => r.Immediate(3)));
        cfg.AddConsumer<GetAccountBalanceHandler>(c =>
            c.UseMessageRetry(r => r.Immediate(3)));
        cfg.AddConsumer<InitLedgerCommandConsumer>(c =>
            c.UseMessageRetry(r => r.Immediate(3)));
        cfg.AddConsumer<RollbackLedgerInitCommandConsumer>(c =>
            c.UseMessageRetry(r => r.Immediate(3)));
        cfg.AddConsumer<ProvisionReportingCommandConsumer>(c =>
            c.UseMessageRetry(r => r.Immediate(3)));
        cfg.AddConsumer<UnprovisionReportingCommandConsumer>(c =>
            c.UseMessageRetry(r => r.Immediate(3)));
        cfg.AddConsumer<LedgerTransactionPostedConsumer>(c =>
        {
            c.UseMessageRetry(r =>
                r.Exponential(
                    5,
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(1)));

            c.UseScheduledRedelivery(r =>
                r.Intervals(
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(2)));
        });
    });
});

WebApplication app = builder.Build();
app.UseSaasBuilderPipeline();

await app.RunAsync().ConfigureAwait(false);

// Expose a partial Program class so WebApplicationFactory<Program> can reference it
// from integration and security test projects (which have InternalsVisibleTo access).
namespace SaasBuilder.Sample.Host
{
    /// <summary>
    /// Partial extension of the compiler-generated Program class so integration tests
    /// can reference it via <c>WebApplicationFactory&lt;Program&gt;</c>.
    /// </summary>
    public partial class Program
    {
    }
}
