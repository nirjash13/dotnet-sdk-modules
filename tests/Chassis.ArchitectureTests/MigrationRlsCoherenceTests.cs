using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Chassis.SharedKernel.Tenancy;
using FluentAssertions;
using Xunit;

namespace Chassis.ArchitectureTests;

/// <summary>
/// Verifies that every entity type implementing <see cref="ITenantScoped"/> in a module's
/// Infrastructure assembly has a corresponding <c>CREATE POLICY</c> statement in that
/// module's SQL migration files.
/// </summary>
/// <remarks>
/// <para>
/// Approach: reflection over Infrastructure assemblies to enumerate <see cref="ITenantScoped"/>
/// entity types, then regex over <c>migrations/{module}/*.sql</c> files.
/// </para>
/// <para>
/// Limitations:
/// <list type="bullet">
///   <item>
///     The test scans for <c>CREATE POLICY</c> statements anywhere in the SQL file — it does not
///     parse SQL AST. A policy for the wrong table would not be detected if the table name appears
///     elsewhere in the same file.
///   </item>
///   <item>
///     Entity types that are explicitly annotated as RLS-exempt (e.g. registration saga state)
///     are skipped. The exclusion list is maintained here and must be updated when new exempt
///     entities are added.
///   </item>
///   <item>
///     The migrations directory is located relative to the repository root by walking up from
///     the test output directory. This works in local builds and on GitHub Actions where the
///     repo is checked out at a consistent path.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class MigrationRlsCoherenceTests
{
    // ── RLS-exempt entity types ───────────────────────────────────────────────────────
    // These types implement ITenantScoped for the global EF Core query filter but intentionally
    // do NOT have Postgres RLS policies (documented in their migration SQL files).
    private static readonly HashSet<Type> RlsExemptTypes =
    [
        // Registration saga state: the tenant being provisioned does not yet exist when the saga
        // starts, so RLS cannot filter on a tenant that has not been created. See
        // migrations/registration/001_initial_registration.sql for the full rationale.
        typeof(Registration.Application.Sagas.RegistrationSagaState),
    ];

    // ── Module assemblies and their migration folder names ───────────────────────────
    // Each entry lists ALL assemblies for a module (Domain + Application + Infrastructure)
    // because ITenantScoped entities may live in Domain (e.g. Account, Posting) or in
    // Application (e.g. TransactionProjection) — not exclusively in Infrastructure.
    private static readonly (string ModuleName, Assembly[] Assemblies)[] ModuleAssemblies =
    [
        ("ledger",
        [
            typeof(Ledger.Domain.Entities.Account).Assembly,                        // Domain entities
            typeof(Ledger.Application.Commands.PostTransactionHandler).Assembly,    // Application
            typeof(Ledger.Infrastructure.Persistence.LedgerDbContext).Assembly,     // Infrastructure
        ]),
        ("identity",
        [
            typeof(Identity.Domain.Entities.User).Assembly,
            typeof(Identity.Application.Services.ICertificateProvider).Assembly,
            typeof(Identity.Infrastructure.Persistence.IdentityDbContext).Assembly,
        ]),
        ("registration",
        [
            // Registration has no Domain project; saga state is in Application.
            typeof(Registration.Application.Sagas.RegistrationSagaState).Assembly,
            typeof(Registration.Infrastructure.Persistence.RegistrationDbContext).Assembly,
        ]),
        ("reporting",
        [
            typeof(Reporting.Application.Persistence.TransactionProjection).Assembly,
            typeof(Reporting.Infrastructure.Persistence.ReportingDbContext).Assembly,
        ]),
    ];

    // ── Test ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_TenantScoped_entity_has_a_CREATE_POLICY_in_its_module_migrations()
    {
        // Load-bearing: a developer adds a new entity implementing ITenantScoped and creates
        // the EF Core migration but forgets to add the Postgres RLS policy. Without this test,
        // the table would be visible to all tenants at the SQL layer while the EF Core filter
        // silently masks the gap in the application layer. This test catches the missing policy
        // before it reaches a deployed environment.
        string migrationsRoot = FindMigrationsRoot();
        var failures = new List<string>();

        foreach ((string moduleName, Assembly[] assemblies) in ModuleAssemblies)
        {
            // Find all ITenantScoped entity types across all assemblies for this module that are not exempt.
            // Deduplicate by type identity in case the same type is visible from multiple assembly references.
            IEnumerable<Type> tenantScopedTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Distinct()
                .Where(t =>
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    typeof(ITenantScoped).IsAssignableFrom(t) &&
                    !RlsExemptTypes.Contains(t));

            if (!tenantScopedTypes.Any())
            {
                continue;
            }

            // Read all SQL migration files for this module.
            string moduleMigrationsPath = Path.Combine(migrationsRoot, moduleName);
            string combinedSql = ReadAllSqlFiles(moduleMigrationsPath, moduleName, failures);

            foreach (Type entityType in tenantScopedTypes)
            {
                // Derive the expected table name.  EF Core by convention pluralises the entity
                // name in snake_case or PascalCase depending on the provider configuration.
                // We check for the entity's short class name (case-insensitive) in the
                // CREATE POLICY statement. This is a best-effort regex; if the table name
                // diverges significantly from the entity name, update the mapping below.
                string entityShortName = entityType.Name.ToLowerInvariant();
                string tableNamePattern = GetExpectedTablePattern(entityType);

                // Match: CREATE POLICY ... ON [schema.]<table>
                bool hasPolicyForTable = Regex.IsMatch(
                    combinedSql,
                    tableNamePattern,
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);

                if (!hasPolicyForTable)
                {
                    failures.Add(
                        $"[{moduleName}] Entity '{entityType.FullName}': no CREATE POLICY statement " +
                        $"found matching table pattern '{tableNamePattern}' in '{moduleMigrationsPath}'. " +
                        "Add an RLS policy for this table or add the entity type to RlsExemptTypes " +
                        "with a documented rationale.");
                }
            }
        }

        failures.Should().BeEmpty(
            because: "Every ITenantScoped entity must have a matching Postgres RLS CREATE POLICY " +
                     "in its module's migration files. Missing policies allow cross-tenant data " +
                     "leakage at the SQL layer when the EF Core global query filter is bypassed.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a regex pattern that matches <c>CREATE POLICY ... ON ... &lt;tableName&gt;</c>
    /// for the given entity type.
    /// </summary>
    private static string GetExpectedTablePattern(Type entityType)
    {
        // Map well-known entity types to their exact table names to avoid fragile string derivation.
        // Add new mappings here when a table name diverges from the entity class name convention.
        var knownTableNames = new Dictionary<Type, string>()
        {
            [typeof(Ledger.Domain.Entities.Account)] = "accounts",
            [typeof(Ledger.Domain.Entities.Posting)] = "postings",
            [typeof(Reporting.Application.Persistence.TransactionProjection)] = "transaction_projections",
        };

        string tableName = knownTableNames.TryGetValue(entityType, out string? mapped)
            ? mapped
            : entityType.Name.ToLowerInvariant();

        // Pattern: CREATE POLICY <name> ON [optional_schema.]<tableName>
        // The table name may be quoted ("accounts") or unquoted (accounts).
        return $@"CREATE\s+POLICY\s+\w+\s+ON\s+(?:\w+\.)?[""']?{Regex.Escape(tableName)}[""']?";
    }

    /// <summary>
    /// Reads all <c>*.sql</c> files in the given directory and returns their combined content.
    /// Appends a failure message if the directory does not exist.
    /// </summary>
    private static string ReadAllSqlFiles(string directoryPath, string moduleName, List<string> failures)
    {
        if (!Directory.Exists(directoryPath))
        {
            failures.Add(
                $"[{moduleName}] Migrations directory not found: '{directoryPath}'. " +
                "Create the directory and add SQL migration files.");
            return string.Empty;
        }

        string[] sqlFiles = Directory.GetFiles(directoryPath, "*.sql", SearchOption.AllDirectories);
        if (sqlFiles.Length == 0)
        {
            return string.Empty;
        }

        return string.Concat(sqlFiles.Select(File.ReadAllText));
    }

    /// <summary>
    /// Locates the <c>migrations/</c> directory by walking up from the test assembly output path.
    /// Stops when it finds a directory containing a <c>migrations</c> subdirectory, or throws.
    /// </summary>
    private static string FindMigrationsRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(MigrationRlsCoherenceTests).Assembly.Location);

        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Cannot locate the 'migrations/' directory by walking up from the test output path. " +
            "Ensure the repository root is an ancestor of the test output directory.");
    }
}
