using System;
using System.Threading.Tasks;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Registration.Application.Sagas;
using Registration.Contracts;
using Xunit;

namespace Chassis.IntegrationTests.Phase5;

/// <summary>
/// Load-bearing integration test: publishes <see cref="AssociationRegistrationStarted"/> and
/// verifies the saga reaches the <c>Completed</c> state.
/// </summary>
/// <remarks>
/// Uses MassTransit's in-process test harness — no Docker / RabbitMQ required.
/// Commands are published (not sent via request/response) — consumers publish their
/// response events back to the bus; the saga listens to those events correlated by CorrelationId.
/// </remarks>
public sealed class RegistrationSagaHappyPathTests
{
    [Fact]
    public async Task PublishStart_WhenAllConsumersSucceed_SagaReachesCompleted()
    {
        // Arrange
        await using ServiceProvider provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<RegistrationSaga, RegistrationSagaState>()
                    .InMemoryRepository();

                // Stub consumers: handle command, publish response event to the bus.
                cfg.AddConsumer<StubCreateUserConsumer>();
                cfg.AddConsumer<StubInitLedgerConsumer>();
                cfg.AddConsumer<StubProvisionReportingConsumer>();
            })
            .BuildServiceProvider(true);

        ITestHarness harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        ISagaStateMachineTestHarness<RegistrationSaga, RegistrationSagaState> sagaHarness =
            harness.GetSagaStateMachineHarness<RegistrationSaga, RegistrationSagaState>();

        var correlationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        await harness.Bus.Publish(new AssociationRegistrationStarted
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            TenantId = tenantId,
            AssociationName = "Test Association",
            PrimaryUserEmail = "admin@test.example",
            Currency = "USD",
            StartedAt = DateTimeOffset.UtcNow,
        });

        // Assert — saga must reach Completed state within harness TestTimeout (default 30s).
        Guid? sagaId = await sagaHarness.Exists(
            correlationId,
            machine => machine.Completed);

        sagaId.Should().NotBeNull("saga must reach the Completed terminal state on happy path");

        await harness.Stop();
    }

    // ── Stub consumers — publish response events (not RespondAsync) ────────

    private sealed class StubCreateUserConsumer : IConsumer<CreateUser>
    {
        public Task Consume(ConsumeContext<CreateUser> context)
            => context.Publish(new UserCreated
            {
                CorrelationId = context.Message.CorrelationId,
                UserId = Guid.NewGuid(),
            });
    }

    private sealed class StubInitLedgerConsumer : IConsumer<InitLedger>
    {
        public Task Consume(ConsumeContext<InitLedger> context)
            => context.Publish(new LedgerInitialized
            {
                CorrelationId = context.Message.CorrelationId,
                AccountId = Guid.NewGuid(),
            });
    }

    private sealed class StubProvisionReportingConsumer : IConsumer<ProvisionReporting>
    {
        public Task Consume(ConsumeContext<ProvisionReporting> context)
            => context.Publish(new ReportingProvisioned
            {
                CorrelationId = context.Message.CorrelationId,
                ReportingId = Guid.NewGuid(),
            });
    }
}
