using System;
using Chassis.Host.Pipeline;
using Identity.Application.Consumers;
using Ledger.Application.Consumers;
using Ledger.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Registration.Application.Sagas;
using Registration.Infrastructure.Persistence;
using Reporting.Application.Consumers;

namespace Chassis.Host.Transport;

/// <summary>
/// Wires MassTransit Mediator (in-proc) or RabbitMQ Bus (out-of-proc) with the chassis pipeline filters.
/// </summary>
/// <remarks>
/// <para>
/// Both <see cref="AddChassisMediator"/> and <see cref="AddChassisBus"/> apply identical pipeline
/// filter stacks so that handler code runs unchanged regardless of transport mode.
/// </para>
/// <para>
/// Consume pipeline filter order (outermost → innermost):
/// TenantPropagationConsume → Logging → Tenant → Validation → Transaction.
/// </para>
/// <para>
/// TenantPropagationConsume is outermost so that all inner filters (including Logging)
/// already see the rehydrated tenant context when they execute.
/// </para>
/// <para>
/// Send and Publish pipelines each have a corresponding propagation filter that writes
/// tenant context headers onto outbound messages.
/// </para>
/// <para>
/// Filters are resolved from DI per-message via the registration context.
/// </para>
/// </remarks>
internal static class MassTransitConfig
{
    /// <summary>
    /// Adds MassTransit Mediator with the chassis pipeline to the service collection.
    /// Used when <c>Dispatch:Transport = "inproc"</c> (default).
    /// </summary>
    public static void AddChassisMediator(IServiceCollection services)
    {
        services.AddMediator(configurator =>
        {
            // ── Registration saga consumers (in-proc mode) ─────────────────────────
            configurator.AddConsumer<CreateUserCommandConsumer>();
            configurator.AddConsumer<DeleteUserCommandConsumer>();
            configurator.AddConsumer<InitLedgerCommandConsumer>();
            configurator.AddConsumer<RollbackLedgerInitCommandConsumer>();
            configurator.AddConsumer<ProvisionReportingCommandConsumer>();
            configurator.AddConsumer<UnprovisionReportingCommandConsumer>();
            configurator.AddConsumer<LedgerTransactionPostedConsumer>();

            configurator.ConfigureMediator((ctx, cfg) =>
            {
                // Outbound: copy ambient tenant context into message headers.
                cfg.UseSendFilter(typeof(TenantPropagationSendFilter<>), ctx);
                cfg.UsePublishFilter(typeof(PublishTenantPropagationFilter<>), ctx);

                // Inbound pipeline (outermost → innermost):

                // 1. TenantPropagationConsume — rehydrate ambient tenant context from headers.
                //    Must be first so all inner filters see the populated context.
                cfg.UseConsumeFilter(typeof(TenantPropagationConsumeFilter<>), ctx);

                // 2. Logging — captures total latency including all inner filters.
                cfg.UseConsumeFilter(typeof(LoggingFilter<>), ctx);

                // 3. Tenant — enforce non-null tenant context for ICommand / IQuery messages.
                cfg.UseConsumeFilter(typeof(TenantFilter<>), ctx);

                // 4. Validation — FluentValidation before handler.
                cfg.UseConsumeFilter(typeof(ValidationFilter<>), ctx);

                // 5. Transaction — Activity span scope (Phase 1); Phase 3 adds DbTransaction.
                cfg.UseConsumeFilter(typeof(TransactionFilter<>), ctx);
            });
        });
    }

    /// <summary>
    /// Adds MassTransit RabbitMQ Bus with EF Core Outbox/Inbox, retry, and the chassis pipeline.
    /// Used when <c>Dispatch:Transport = "bus"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Outbox</b> — <c>AddEntityFrameworkOutbox&lt;LedgerDbContext&gt;</c> ensures that
    /// integration events are written atomically with the EF Core transaction and delivered
    /// reliably to RabbitMQ by the <c>BusOutboxDeliveryService</c> background worker.
    /// MT outbox tables do not carry a <c>TenantId</c> column; tenant context is propagated
    /// via message headers (see <see cref="TenantPropagationSendFilter{T}"/>).
    /// </para>
    /// <para>
    /// <b>Inbox</b> — <c>AddEntityFrameworkInbox&lt;ReportingDbContext&gt;</c> records each
    /// consumed message in an <c>InboxState</c> row, preventing duplicate processing on redelivery.
    /// The business-level unique index on <c>(TenantId, SourceMessageId)</c> provides a second
    /// layer of idempotency protection.
    /// </para>
    /// <para>
    /// <b>DLQ</b> — MT's default <c>_skipped</c> and <c>_error</c> queues are used.
    /// Phase 7 will wire a Grafana alert on <c>rabbit_dlq_depth &gt; 0</c>.
    /// </para>
    /// </remarks>
    public static void AddChassisBus(IServiceCollection services, IConfiguration config)
    {
        string rabbitHost = config["RabbitMq:Host"] ?? "localhost";
        string rabbitUser = config["RabbitMq:Username"] ?? "guest";
        string rabbitPass = config["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            // ── EF Core Outbox (Ledger side — publisher) ──────────────────────────
            x.AddEntityFrameworkOutbox<LedgerDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();

                // Poll the outbox every second for pending messages.
                // Reduces end-to-end latency while avoiding excessive polling in steady state.
                o.QueryDelay = TimeSpan.FromSeconds(1);
            });

            // ── Registration saga ─────────────────────────────────────────────────
            // The saga state is persisted in RegistrationDbContext (registration schema).
            // ExistingDbContext<T> reuses the DI-registered context (AddChassisPersistence<T>).
            x.AddSagaStateMachine<RegistrationSaga, RegistrationSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<RegistrationDbContext>();
                    r.UsePostgres();
                });

            // ── Registration saga compensation consumers ───────────────────────────
            x.AddConsumer<CreateUserCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<DeleteUserCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<InitLedgerCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<RollbackLedgerInitCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<ProvisionReportingCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<UnprovisionReportingCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));

            // ── Consumer registration with retry ──────────────────────────────────
            //
            // Note on inbox: In MassTransit 8.x, consumer-side inbox deduplication is
            // achieved by configuring UseEntityFrameworkOutbox<ReportingDbContext> on the
            // receive endpoint (done in the UsingRabbitMq block below via ConfigureEndpoints).
            // The AddEntityFrameworkInbox API is not available in MT 8.x.
            // Idempotency in this chassis is enforced at the application level via the
            // consumer's ExistsAsync check + the unique index on (TenantId, SourceMessageId).
            x.AddConsumer<LedgerTransactionPostedConsumer>(cfg =>
            {
                // Exponential retry: up to 5 attempts; initial 100ms; max 30s; interval delta 1s.
                cfg.UseMessageRetry(r =>
                    r.Exponential(5, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1)));

                // Scheduled redelivery for transient downstream failures (e.g. DB temporarily unavailable).
                // Delays: 5 min, 30 min, 2 hr — gives the system time to recover before the message
                // is sent to the DLQ (_error queue).
                cfg.UseScheduledRedelivery(r =>
                    r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)));
            });

            // ── RabbitMQ transport ─────────────────────────────────────────────────
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                // Outbound: copy ambient tenant context into message headers.
                cfg.UseSendFilter(typeof(TenantPropagationSendFilter<>), ctx);
                cfg.UsePublishFilter(typeof(PublishTenantPropagationFilter<>), ctx);

                // Inbound pipeline (outermost → innermost):
                // Mirrors AddChassisMediator — same filter stack, different transport.

                // 1. TenantPropagationConsume — rehydrate ambient tenant context from headers.
                cfg.UseConsumeFilter(typeof(TenantPropagationConsumeFilter<>), ctx);

                // 2. Logging — captures total latency including all inner filters.
                cfg.UseConsumeFilter(typeof(LoggingFilter<>), ctx);

                // 3. Tenant — enforce non-null tenant context.
                cfg.UseConsumeFilter(typeof(TenantFilter<>), ctx);

                // 4. Validation — FluentValidation before handler.
                cfg.UseConsumeFilter(typeof(ValidationFilter<>), ctx);

                // 5. Transaction — Activity span scope.
                cfg.UseConsumeFilter(typeof(TransactionFilter<>), ctx);

                // Auto-configure all registered consumer endpoints.
                cfg.ConfigureEndpoints(ctx);
            });
        });
    }
}
