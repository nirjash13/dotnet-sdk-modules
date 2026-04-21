using System;
using MassTransit;
using Registration.Contracts;

namespace Registration.Application.Sagas;

/// <summary>
/// MassTransit state machine saga that orchestrates association registration
/// across the Identity, Ledger, and Reporting modules with full compensation on failure.
/// </summary>
/// <remarks>
/// <para>
/// Happy path: <c>AssociationRegistrationStarted</c> → <c>CreateUser</c> → <c>UserCreated</c>
/// → <c>InitLedger</c> → <c>LedgerInitialized</c> → <c>ProvisionReporting</c>
/// → <c>ReportingProvisioned</c> → publishes <c>RegistrationCompleted</c>.
/// </para>
/// <para>
/// Failure path: any <c>Fault&lt;T&gt;</c> transitions to <c>Compensating</c>; compensation
/// commands are published in reverse order for steps already completed; terminal state is <c>Faulted</c>.
/// </para>
/// <para>
/// Commands are published (not sent) so that they route to registered consumers regardless of
/// transport topology. This allows the in-process test harness to route commands correctly without
/// a pre-configured send endpoint URI.
/// </para>
/// <para>
/// Saga state is tenant-agnostic — see <see cref="RegistrationSagaState"/> remarks.
/// </para>
/// </remarks>
public sealed class RegistrationSaga : MassTransitStateMachine<RegistrationSagaState>
{
    // ── States ────────────────────────────────────────────────────────────
    // MT uses reflection to wire State properties from property names.

    /// <summary>Saga is creating the Identity user.</summary>
    public State CreatingUser { get; private set; } = null!;

    /// <summary>Saga is initializing the Ledger account.</summary>
    public State InitializingLedger { get; private set; } = null!;

    /// <summary>Saga is provisioning Reporting.</summary>
    public State ProvisioningReporting { get; private set; } = null!;

    /// <summary>Saga completed successfully.</summary>
    public State Completed { get; private set; } = null!;

    /// <summary>Saga is rolling back already-completed steps.</summary>
    public State Compensating { get; private set; } = null!;

    /// <summary>Saga ended in a faulted (non-recoverable) terminal state.</summary>
    public State Faulted { get; private set; } = null!;

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Trigger: external event published by the Registration API.</summary>
    public Event<AssociationRegistrationStarted> RegistrationStarted { get; private set; } = null!;

    /// <summary>Happy-path response from Identity.</summary>
    public Event<UserCreated> UserCreatedEvent { get; private set; } = null!;

    /// <summary>Happy-path response from Ledger.</summary>
    public Event<LedgerInitialized> LedgerInitializedEvent { get; private set; } = null!;

    /// <summary>Happy-path response from Reporting.</summary>
    public Event<ReportingProvisioned> ReportingProvisionedEvent { get; private set; } = null!;

    /// <summary>Fault raised when <see cref="CreateUser"/> fails.</summary>
    public Event<Fault<CreateUser>> CreateUserFaulted { get; private set; } = null!;

    /// <summary>Fault raised when <see cref="InitLedger"/> fails.</summary>
    public Event<Fault<InitLedger>> InitLedgerFaulted { get; private set; } = null!;

    /// <summary>Fault raised when <see cref="ProvisionReporting"/> fails.</summary>
    public Event<Fault<ProvisionReporting>> ProvisionReportingFaulted { get; private set; } = null!;

    /// <summary>Response from Identity when compensation (DeleteUser) completes.</summary>
    public Event<UserDeleted> UserDeletedEvent { get; private set; } = null!;

    /// <summary>Response from Ledger when compensation (RollbackLedgerInit) completes.</summary>
    public Event<LedgerRolledBack> LedgerRolledBackEvent { get; private set; } = null!;

    /// <summary>Initializes the saga state machine, wiring states, events, and transitions.</summary>
    public RegistrationSaga()
    {
        // ── State property mapping ────────────────────────────────────────
        InstanceState(x => x.CurrentState);

        // ── Event correlation — all events carry CorrelationId ────────────
        Event(() => RegistrationStarted, e => e.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => UserCreatedEvent, e => e.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => LedgerInitializedEvent, e => e.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => ReportingProvisionedEvent, e => e.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => CreateUserFaulted, e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));
        Event(() => InitLedgerFaulted, e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));
        Event(() => ProvisionReportingFaulted, e => e.CorrelateById(ctx => ctx.Message.Message.CorrelationId));
        Event(() => UserDeletedEvent, e => e.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => LedgerRolledBackEvent, e => e.CorrelateById(ctx => ctx.Message.CorrelationId));

        // ── Initial → CreatingUser ────────────────────────────────────────
        Initially(
            When(RegistrationStarted)
                .Then(ctx =>
                {
                    ctx.Saga.TenantId = ctx.Message.TenantId;
                    ctx.Saga.AssociationName = ctx.Message.AssociationName;
                    ctx.Saga.PrimaryUserEmail = ctx.Message.PrimaryUserEmail;
                    ctx.Saga.Currency = ctx.Message.Currency;
                    ctx.Saga.StartedAt = ctx.Message.StartedAt;
                })
                .Publish(ctx => new CreateUser
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    TenantId = ctx.Saga.TenantId,
                    Email = ctx.Saga.PrimaryUserEmail,
                    DisplayName = ctx.Saga.AssociationName,
                })
                .TransitionTo(CreatingUser));

        // ── CreatingUser → InitializingLedger (happy) ─────────────────────
        During(
            CreatingUser,
            When(UserCreatedEvent)
                .Then(ctx => ctx.Saga.UserId = ctx.Message.UserId)
                .Publish(ctx => new InitLedger
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    TenantId = ctx.Saga.TenantId,
                    Currency = ctx.Saga.Currency,
                })
                .TransitionTo(InitializingLedger),
            When(CreateUserFaulted)
                .Then(ctx =>
                    ctx.Saga.FailureReason = $"CreateUser faulted: {ctx.Message.Exceptions?[0].Message}")
                .TransitionTo(Faulted)
                .Publish(ctx => new RegistrationFailed
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    TenantId = ctx.Saga.TenantId,
                    FailureReason = ctx.Saga.FailureReason ?? "CreateUser faulted",
                    FailedAt = DateTimeOffset.UtcNow,
                }));

        // ── InitializingLedger → ProvisioningReporting (happy) ────────────
        During(
            InitializingLedger,
            When(LedgerInitializedEvent)
                .Then(ctx => ctx.Saga.AccountId = ctx.Message.AccountId)
                .Publish(ctx => new ProvisionReporting
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    TenantId = ctx.Saga.TenantId,
                })
                .TransitionTo(ProvisioningReporting),
            When(InitLedgerFaulted)
                .Then(ctx =>
                    ctx.Saga.FailureReason = $"InitLedger faulted: {ctx.Message.Exceptions?[0].Message}")
                .TransitionTo(Compensating)
                .Publish(ctx => new DeleteUser
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    UserId = ctx.Saga.UserId ?? Guid.Empty,
                }));

        // ── ProvisioningReporting → Completed (happy) ─────────────────────
        During(
            ProvisioningReporting,
            When(ReportingProvisionedEvent)
                .Then(ctx =>
                {
                    ctx.Saga.ReportingId = ctx.Message.ReportingId;
                    ctx.Saga.CompletedAt = DateTimeOffset.UtcNow;
                })
                .Publish(ctx => new RegistrationCompleted
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    TenantId = ctx.Saga.TenantId,
                    UserId = ctx.Saga.UserId ?? Guid.Empty,
                    AccountId = ctx.Saga.AccountId ?? Guid.Empty,
                    ReportingId = ctx.Saga.ReportingId ?? Guid.Empty,
                    CompletedAt = ctx.Saga.CompletedAt ?? DateTimeOffset.UtcNow,
                })
                .TransitionTo(Completed),
            When(ProvisionReportingFaulted)
                .Then(ctx =>
                    ctx.Saga.FailureReason = $"ProvisionReporting faulted: {ctx.Message.Exceptions?[0].Message}")
                .TransitionTo(Compensating)
                .Publish(ctx => new RollbackLedgerInit
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    AccountId = ctx.Saga.AccountId ?? Guid.Empty,
                    TenantId = ctx.Saga.TenantId,
                }));

        // ── Compensating — wait for rollback responses ────────────────────
        During(
            Compensating,
            When(LedgerRolledBackEvent)
                .Publish(ctx => new DeleteUser
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    UserId = ctx.Saga.UserId ?? Guid.Empty,
                }),
            When(UserDeletedEvent)
                .Then(ctx => ctx.Saga.CompletedAt = DateTimeOffset.UtcNow)
                .Publish(ctx => new RegistrationFailed
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    TenantId = ctx.Saga.TenantId,
                    FailureReason = ctx.Saga.FailureReason ?? "Compensation completed",
                    FailedAt = ctx.Saga.CompletedAt ?? DateTimeOffset.UtcNow,
                })
                .TransitionTo(Faulted));

        // Mark Completed and Faulted as terminal so MT does not keep the saga instance alive.
        SetCompletedWhenFinalized();
    }
}
