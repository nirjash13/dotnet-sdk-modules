using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Chassis.IntegrationTests;

/// <summary>
/// Architecture rule tests enforcing layer boundary invariants.
/// Phase 1 scope: SharedKernel must have zero EF Core dependencies.
/// </summary>
/// <remarks>
/// Design note: SharedKernel's net10.0 facet intentionally references ASP.NET Core via
/// <c>FrameworkReference</c> to expose <c>IEndpointRouteBuilder</c> in <c>IModuleStartup.Configure</c>.
/// This is guarded with <c>#if NET10_0_OR_GREATER</c> so the netstandard2.0 facet remains
/// infrastructure-free. The architecture test therefore only enforces that EF Core is absent —
/// the ASP.NET Core reference is an approved design decision (CHANGELOG_AI.md Phase 0).
/// </remarks>
public sealed class ArchitectureRuleTests
{
    [Fact]
    public void SharedKernel_MustNotDependOn_EfCore()
    {
        // Arrange
        Types types = Types.InAssembly(typeof(Chassis.SharedKernel.Tenancy.ITenantScoped).Assembly);

        // Act
        TestResult result = types
            .That()
            .ResideInNamespaceStartingWith("Chassis.SharedKernel")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.Relational",
                "Microsoft.EntityFrameworkCore.Infrastructure")
            .GetResult();

        // Assert
        string failingTypes = result.FailingTypeNames is not null
            ? string.Join(", ", result.FailingTypeNames)
            : "none";

        result.IsSuccessful.Should().BeTrue(
            because: $"SharedKernel must not depend on EF Core — it targets netstandard2.0 for .NET 4.x consumers. Failing types: {failingTypes}");
    }
}
