using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Chassis.Persistence;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Chassis.ArchitectureTests;

/// <summary>
/// Enforces layer-boundary invariants for all modules in the Modular SaaS Chassis.
/// Each test targets a distinct architectural rule; a broken rule causes exactly one test to fail.
/// </summary>
public sealed class LayerBoundaryTests
{
    // ── Assemblies ─────────────────────────────────────────────────────────────────────

    // Domain assemblies — must have zero infrastructure dependencies.
    private static readonly Assembly[] DomainAssemblies =
    [
        typeof(Ledger.Domain.Entities.Account).Assembly,
        typeof(Identity.Domain.Entities.User).Assembly,
        // Registration has no dedicated Domain project — saga state is in Application.
        // Reporting has no dedicated Domain project — projection is in Application.
    ];

    // Application assemblies — must not reference their own module's Infrastructure.
    private static readonly (Assembly Application, string InfrastructureNamespace)[] ApplicationInfraPairs =
    [
        (typeof(Ledger.Application.Commands.PostTransactionHandler).Assembly, "Ledger.Infrastructure"),
        (typeof(Identity.Application.Services.ICertificateProvider).Assembly, "Identity.Infrastructure"),
        (typeof(Registration.Application.Sagas.RegistrationSagaState).Assembly, "Registration.Infrastructure"),
        (typeof(Reporting.Application.Persistence.TransactionProjection).Assembly, "Reporting.Infrastructure"),
    ];

    // All module Infrastructure assemblies — for cross-module isolation test.
    private static readonly (string ModuleName, Assembly Infrastructure)[] InfrastructureAssemblies =
    [
        ("Ledger", typeof(Ledger.Infrastructure.Persistence.LedgerDbContext).Assembly),
        ("Identity", typeof(Identity.Infrastructure.Persistence.IdentityDbContext).Assembly),
        ("Registration", typeof(Registration.Infrastructure.Persistence.RegistrationDbContext).Assembly),
        ("Reporting", typeof(Reporting.Infrastructure.Persistence.ReportingDbContext).Assembly),
    ];

    // ── Test 1: Domain assemblies reference none of the prohibited infrastructure namespaces ──

    [Fact]
    public void Domain_projects_reference_none_of_EFCore_AspNetCore_MassTransit_FluentValidation_OpenIddict()
    {
        // Load-bearing: if a developer accidentally adds an EF Core or MassTransit dependency to
        // a Domain project, this test catches it before it reaches CI. The domain layer's
        // isolation guarantee — zero transitive infrastructure coupling — is the primary invariant.
        string[] prohibitedNamespaces =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "MassTransit",
            "FluentValidation",
            "OpenIddict",
        ];

        var failures = new List<string>();

        foreach (Assembly assembly in DomainAssemblies)
        {
            TestResult result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(prohibitedNamespaces)
                .GetResult();

            if (!result.IsSuccessful)
            {
                string failingTypes = result.FailingTypeNames is not null
                    ? string.Join(", ", result.FailingTypeNames)
                    : "none";

                failures.Add(
                    $"Assembly '{assembly.GetName().Name}' has prohibited dependencies. Failing types: {failingTypes}");
            }
        }

        failures.Should().BeEmpty(
            because: "Domain layers must have zero dependencies on EF Core, ASP.NET Core, MassTransit, FluentValidation, or OpenIddict. " +
                     "These layers target net10.0 but must remain infrastructure-free to preserve testability and portability.");
    }

    // ── Test 2: Application layer does not reference its own module's Infrastructure ──

    [Fact]
    public void Application_layer_has_no_Infrastructure_references()
    {
        // Load-bearing: Application referencing Infrastructure would invert the dependency
        // arrow — Infrastructure must implement Application interfaces, not vice versa.
        // A regression here means business logic becomes coupled to EF Core / Npgsql.
        var failures = new List<string>();

        foreach ((Assembly applicationAssembly, string infraNamespace) in ApplicationInfraPairs)
        {
            TestResult result = Types.InAssembly(applicationAssembly)
                .ShouldNot()
                .HaveDependencyOn(infraNamespace)
                .GetResult();

            if (!result.IsSuccessful)
            {
                string failingTypes = result.FailingTypeNames is not null
                    ? string.Join(", ", result.FailingTypeNames)
                    : "none";

                failures.Add(
                    $"Assembly '{applicationAssembly.GetName().Name}' references '{infraNamespace}'. " +
                    $"Failing types: {failingTypes}");
            }
        }

        failures.Should().BeEmpty(
            because: "Application layers must not reference their module's Infrastructure assembly. " +
                     "Infrastructure implements Application interfaces — never the reverse.");
    }

    // ── Test 3: No cross-module Infrastructure references ──────────────────────────────

    [Fact]
    public void No_cross_module_Infrastructure_references()
    {
        // Load-bearing: a cross-infrastructure reference (e.g. Ledger.Infrastructure referencing
        // Reporting.Infrastructure) would couple two bounded contexts at the data layer —
        // exactly the anti-pattern this modular architecture prevents.
        var failures = new List<string>();

        foreach ((string moduleName, Assembly infraAssembly) in InfrastructureAssemblies)
        {
            foreach ((string otherModule, Assembly otherInfra) in InfrastructureAssemblies)
            {
                if (string.Equals(moduleName, otherModule, StringComparison.Ordinal))
                {
                    continue;
                }

                string otherNamespace = otherInfra.GetName().Name!;

                TestResult result = Types.InAssembly(infraAssembly)
                    .ShouldNot()
                    .HaveDependencyOn(otherNamespace)
                    .GetResult();

                if (!result.IsSuccessful)
                {
                    string failingTypes = result.FailingTypeNames is not null
                        ? string.Join(", ", result.FailingTypeNames)
                        : "none";

                    failures.Add(
                        $"'{moduleName}.Infrastructure' references '{otherModule}.Infrastructure'. " +
                        $"Failing types: {failingTypes}");
                }
            }
        }

        failures.Should().BeEmpty(
            because: "Infrastructure assemblies must be isolated from each other. " +
                     "Cross-module data coupling bypasses the bounded-context contract (Contracts projects). " +
                     "Use integration events / Contracts projects for cross-module communication.");
    }

    // ── Test 4: All DbContexts inherit ChassisDbContext ────────────────────────────────

    [Fact]
    public void All_DbContexts_inherit_ChassisDbContext()
    {
        // Load-bearing: a DbContext that bypasses ChassisDbContext loses the automatic tenant
        // global query filter. A developer adding a new DbContext without inheriting from the
        // base would silently expose all tenants' data through EF Core queries.
        Type chassisDbContextType = typeof(ChassisDbContext);

        // Collect all concrete DbContext subclasses from all Infrastructure assemblies.
        var nonChassisDbContexts = new List<string>();

        foreach ((string moduleName, Assembly infraAssembly) in InfrastructureAssemblies)
        {
            IEnumerable<Type> dbContextTypes = infraAssembly.GetTypes()
                .Where(t =>
                    !t.IsAbstract &&
                    typeof(Microsoft.EntityFrameworkCore.DbContext).IsAssignableFrom(t));

            foreach (Type dbContextType in dbContextTypes)
            {
                if (!chassisDbContextType.IsAssignableFrom(dbContextType))
                {
                    nonChassisDbContexts.Add(
                        $"[{moduleName}] {dbContextType.FullName} does not inherit ChassisDbContext");
                }
            }
        }

        nonChassisDbContexts.Should().BeEmpty(
            because: "Every module DbContext must inherit from ChassisDbContext to receive the automatic " +
                     "tenant global query filter. A context bypassing this base loses tenant isolation at " +
                     "the EF Core layer (RLS on the DB side is the only remaining defence).");
    }

    // ── Test 5: All Contracts projects multitarget netstandard2.0 and net10.0 ─────────

    [Fact]
    public void All_Contracts_assemblies_are_net_standard_2_compatible()
    {
        // Load-bearing: Contracts projects are consumed by .NET Framework 4.8 legacy services
        // via the Integration bridge. If a Contracts assembly drops netstandard2.0 targeting,
        // the legacy service cannot reference it and the integration contract breaks silently.
        //
        // Implementation note: we cannot inspect the TargetFrameworks MSBuild property at runtime.
        // Instead we verify that each Contracts assembly's ImageRuntimeVersion is compatible with
        // netstandard2.0 (i.e. compiled as netstandard or a compatible TFM). The reliable
        // signal at runtime is the assembly's custom attributes — specifically the
        // System.Runtime.Versioning.TargetFrameworkAttribute.
        //
        // Since the test project links against the net10.0 facet of the contracts assemblies,
        // we verify that the netstandard2.0 build output also exists on disk (the csproj sets
        // both TFMs, so both output directories must be present after a successful build).
        Assembly[] contractsAssemblies =
        [
            typeof(Ledger.Contracts.LedgerTransactionPosted).Assembly,
            typeof(Identity.Contracts.TenantMembershipCreated).Assembly,
            typeof(Registration.Contracts.AssociationRegistrationStarted).Assembly,
            typeof(Reporting.Contracts.TransactionProjectionDto).Assembly,
        ];

        var missingNetStandardBuilds = new List<string>();
        string repoRoot = FindRepoRoot();

        foreach (Assembly contractsAssembly in contractsAssemblies)
        {
            string assemblyName = contractsAssembly.GetName().Name!;

            // Locate the source project's bin directory.
            // Pattern: {repoRoot}/src/Modules/{Module}/{Module}.Contracts/bin/Debug/netstandard2.0/
            // We search under src/ for a netstandard2.0 output directory containing this dll.
            string srcRoot = System.IO.Path.Combine(repoRoot, "src");
            string expectedDllName = assemblyName + ".dll";

            string? netStandardDll = FindFileUnderDirectory(
                srcRoot,
                $"netstandard2.0/{expectedDllName}");

            if (netStandardDll is null)
            {
                missingNetStandardBuilds.Add(
                    $"'{assemblyName}': no netstandard2.0 build found under '{srcRoot}'. " +
                    "Verify <TargetFrameworks> includes netstandard2.0 and that a build has been run. " +
                    "Expected a file matching pattern: src/**/*.Contracts/bin/*/netstandard2.0/*.dll");
            }
        }

        missingNetStandardBuilds.Should().BeEmpty(
            because: "All Contracts projects must multitarget netstandard2.0;net10.0 so that .NET Framework " +
                     "4.8 integration bridge projects can reference the same event/DTO types. " +
                     "A missing netstandard2.0 build means legacy consumers cannot share the contract.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Locates the repository root by walking up from the test assembly output path until
    /// it finds a directory containing a <c>Chassis.sln</c> file.
    /// </summary>
    private static string FindRepoRoot()
    {
        string? dir = System.IO.Path.GetDirectoryName(typeof(LayerBoundaryTests).Assembly.Location);

        while (dir is not null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir, "Chassis.sln")))
            {
                return dir;
            }

            dir = System.IO.Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Cannot locate the repository root (directory containing Chassis.sln) by walking " +
            "up from the test output path.");
    }

    /// <summary>
    /// Searches recursively under <paramref name="rootDir"/> for a file whose path ends with
    /// <paramref name="relativePattern"/> (forward-slash-separated). Returns the first match
    /// or <see langword="null"/> if not found.
    /// </summary>
    private static string? FindFileUnderDirectory(string rootDir, string relativePattern)
    {
        if (!System.IO.Directory.Exists(rootDir))
        {
            return null;
        }

        // Normalise to forward slashes for cross-platform pattern matching.
        string normalisedPattern = relativePattern.Replace('\\', '/');

        foreach (string file in System.IO.Directory.EnumerateFiles(rootDir, "*", System.IO.SearchOption.AllDirectories))
        {
            string normalisedFile = file.Replace('\\', '/');
            if (normalisedFile.EndsWith(normalisedPattern, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }
}
