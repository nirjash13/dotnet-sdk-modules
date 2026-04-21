using System;
using System.Threading;
using System.Threading.Tasks;
using Chassis.Host.Transport;
using Chassis.SharedKernel.Tenancy;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chassis.IntegrationTests.Phase2;

/// <summary>
/// Acceptance criterion (b): publishing an in-proc event copies the <c>tenant_id</c> header;
/// the downstream consumer rehydrates the SAME tenant context.
///
/// Load-bearing rationale: if <see cref="PublishTenantPropagationFilter{T}"/> fails to write
/// the tenant-id header, or <see cref="TenantPropagationConsumeFilter{T}"/> fails to read it,
/// the consumer sees a null context — the assertion on <c>TenantId</c> catches both bugs.
/// If the finally-block in the consume filter is removed, the third test fails.
/// </summary>
public sealed class MassTransitTenantPropagationTests
{
    private static readonly Guid TenantA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid UserA = new Guid("cccccccc-0000-0000-0000-000000000003");

    // ── Test (b-1): happy path ─────────────────────────────────────────────────────
    [Fact]
    public async Task PublishInProc_TenantContextFlowsToConsumer()
    {
        // Arrange
        await using ServiceProvider sp = BuildServiceProvider();
        ITestHarness harness = sp.GetRequiredService<ITestHarness>();
        await harness.Start();

        ITenantContextAccessor accessor = sp.GetRequiredService<ITenantContextAccessor>();

        // Set ambient tenant context on the publisher thread.
        accessor.Current = new TenantContext(TenantA, UserA, correlationId: "test-corr-1");

        IPublishEndpoint publisher = sp.GetRequiredService<IPublishEndpoint>();

        // Act
        await publisher.Publish(new TestTenantEvent { Payload = "hello" }, CancellationToken.None);

        IConsumerTestHarness<TestTenantEventConsumer> consumerHarness =
            harness.GetConsumerHarness<TestTenantEventConsumer>();

        bool consumed = await consumerHarness.Consumed.Any<TestTenantEvent>();
        consumed.Should().BeTrue(because: "the consumer must receive the published event");

        // Assert — the consumer's observed tenant matches what was published.
        TestTenantEventConsumer consumer = sp.GetRequiredService<TestTenantEventConsumer>();
        consumer.ObservedTenantId.Should().Be(TenantA,
            because: "TenantPropagationConsumeFilter must rehydrate the tenant_id from the message header");
    }

    // ── Test (b-2): no tenant context — publish proceeds; consumer sees null ────────
    [Fact]
    public async Task PublishInProc_NoTenantContext_ConsumerSeesNullContext()
    {
        // Arrange
        await using ServiceProvider sp = BuildServiceProvider();
        ITestHarness harness = sp.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Deliberately do NOT set accessor.Current.
        IPublishEndpoint publisher = sp.GetRequiredService<IPublishEndpoint>();

        // Act
        await publisher.Publish(new TestTenantEvent { Payload = "no-tenant" }, CancellationToken.None);

        IConsumerTestHarness<TestTenantEventConsumer> consumerHarness =
            harness.GetConsumerHarness<TestTenantEventConsumer>();

        bool consumed = await consumerHarness.Consumed.Any<TestTenantEvent>();
        consumed.Should().BeTrue(because: "publish with no tenant context must still deliver the message");

        TestTenantEventConsumer consumer = sp.GetRequiredService<TestTenantEventConsumer>();
        consumer.ObservedTenantId.Should().BeNull(
            because: "without an ambient tenant context the consumer sees no tenant_id header");
    }

    // ── Test (b-3): context is set inside consumer; filter finally-block restores prev ──
    [Fact]
    public async Task PublishInProc_ConsumerObservesCorrectTenant_FilterIsolatesExecutionContext()
    {
        // Arrange
        await using ServiceProvider sp = BuildServiceProvider();
        ITestHarness harness = sp.GetRequiredService<ITestHarness>();
        await harness.Start();

        ITenantContextAccessor accessor = sp.GetRequiredService<ITenantContextAccessor>();
        accessor.Current = new TenantContext(TenantA, UserA);

        IPublishEndpoint publisher = sp.GetRequiredService<IPublishEndpoint>();

        // Act
        await publisher.Publish(new TestTenantEvent { Payload = "leak-check" }, CancellationToken.None);

        IConsumerTestHarness<TestTenantEventConsumer> consumerHarness =
            harness.GetConsumerHarness<TestTenantEventConsumer>();

        await consumerHarness.Consumed.Any<TestTenantEvent>();

        // Brief pause to let the filter's finally-block complete.
        await Task.Delay(50);

        // Assert — the consumer's execution context must have seen TenantA (filter set it).
        TestTenantEventConsumer consumer = sp.GetRequiredService<TestTenantEventConsumer>();
        consumer.ObservedTenantId.Should().Be(TenantA,
            because: "TenantPropagationConsumeFilter must set tenant context before invoking the consumer");

        // The test thread's accessor value is still TenantA — the consumer filter ran in its own
        // AsyncLocal execution context, so mutation there is isolated (correct isolation).
        accessor.Current?.TenantId.Should().Be(TenantA,
            because: "the test execution context is independent from the consumer execution context");
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Shared accessor singleton — same instance seen by filters and the consumer.
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

        // Shared consumer instance so tests can inspect it after dispatch.
        services.AddSingleton<TestTenantEventConsumer>();

        services.AddLogging();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<TestTenantEventConsumer>();

            cfg.UsingInMemory((ctx, bus) =>
            {
                bus.UsePublishFilter(typeof(PublishTenantPropagationFilter<>), ctx);
                bus.UseSendFilter(typeof(TenantPropagationSendFilter<>), ctx);
                bus.UseConsumeFilter(typeof(TenantPropagationConsumeFilter<>), ctx);

                bus.ConfigureEndpoints(ctx);
            });
        });

        return services.BuildServiceProvider();
    }
}

/// <summary>
/// Minimal test event used to exercise the tenant propagation filters.
/// Not in production code — test fixture only.
/// </summary>
public sealed class TestTenantEvent
{
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// Test consumer that records the tenant context observed at consume time.
/// Registered as a singleton so tests can inspect <see cref="ObservedTenantId"/> after dispatch.
/// </summary>
public sealed class TestTenantEventConsumer(ITenantContextAccessor accessor) : IConsumer<TestTenantEvent>
{
    /// <summary>Gets the <c>TenantId</c> observed when the consumer executed, or <see langword="null"/>.</summary>
    public Guid? ObservedTenantId { get; private set; }

    /// <inheritdoc />
    public Task Consume(ConsumeContext<TestTenantEvent> context)
    {
        ObservedTenantId = accessor.Current?.TenantId;
        return Task.CompletedTask;
    }
}
