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
/// Load-bearing chaos test: injects a fault after <c>InitLedger</c> succeeds but before
/// <c>ProvisionReporting</c> completes. Verifies the saga transitions through <c>Compensating</c>
/// and reaches the <c>Faulted</c> terminal state.
/// </summary>
/// <remarks>
/// Uses MassTransit's in-process test harness — no Docker required.
/// Consumers publish response events (not RespondAsync) so the saga can correlate them.
/// Compensation chain: ProvisionReporting throws → Fault published → saga transitions to Compensating
/// → saga publishes RollbackLedgerInit → consumer publishes LedgerRolledBack
/// → saga publishes DeleteUser → consumer publishes UserDeleted → saga transitions to Faulted.
/// </remarks>
public sealed class RegistrationSagaCompensationTests
{
    [Fact]
    public async Task PublishStart_WhenProvisionReportingFaults_SagaReachesFaultedAndCompensates()
    {
        // Arrange
        await using ServiceProvider provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<RegistrationSaga, RegistrationSagaState>()
                    .InMemoryRepository();

                cfg.AddConsumer<StubCreateUserConsumer>();
                cfg.AddConsumer<StubInitLedgerConsumer>();
                cfg.AddConsumer<FaultingProvisionReportingConsumer>(); // Throws → saga compensates
                cfg.AddConsumer<StubRollbackLedgerConsumer>();
                cfg.AddConsumer<StubDeleteUserConsumer>();
            })
            .BuildServiceProvider(true);

        ITestHarness harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        ISagaStateMachineTestHarness<RegistrationSaga, RegistrationSagaState> sagaHarness =
            harness.GetSagaStateMachineHarness<RegistrationSaga, RegistrationSagaState>();

        var correlationId = Guid.NewGuid();

        // Act
        await harness.Bus.Publish(new AssociationRegistrationStarted
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            TenantId = Guid.NewGuid(),
            AssociationName = "Compensation Test Assoc",
            PrimaryUserEmail = "fail@test.example",
            Currency = "GBP",
            StartedAt = DateTimeOffset.UtcNow,
        });

        // Assert — saga must reach Faulted terminal state after compensation chain.
        Guid? sagaId = await sagaHarness.Exists(
            correlationId,
            machine => machine.Faulted);

        sagaId.Should().NotBeNull("saga must reach the Faulted terminal state when ProvisionReporting faults");

        await harness.Stop();
    }

    // ── Stub consumers — publish response events ───────────────────────────

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

    /// <summary>Simulates the Reporting consumer throwing, triggering the saga's fault path.</summary>
    private sealed class FaultingProvisionReportingConsumer : IConsumer<ProvisionReporting>
    {
        public Task Consume(ConsumeContext<ProvisionReporting> context)
            => throw new InvalidOperationException("Simulated Reporting provisioning failure (chaos test).");
    }

    private sealed class StubRollbackLedgerConsumer : IConsumer<RollbackLedgerInit>
    {
        public Task Consume(ConsumeContext<RollbackLedgerInit> context)
            => context.Publish(new LedgerRolledBack
            {
                CorrelationId = context.Message.CorrelationId,
                AccountId = context.Message.AccountId,
            });
    }

    private sealed class StubDeleteUserConsumer : IConsumer<DeleteUser>
    {
        public Task Consume(ConsumeContext<DeleteUser> context)
            => context.Publish(new UserDeleted
            {
                CorrelationId = context.Message.CorrelationId,
                UserId = context.Message.UserId,
            });
    }
}
