namespace SaasBuilder.Cli.Scaffolding;

/// <summary>
/// Scaffolds a new vertical-slice module under <c>src/Modules/&lt;name&gt;/</c>.
/// Emits five projects: Contracts, Domain, Application, Infrastructure, Api.
/// </summary>
internal static class ModuleScaffolder
{
    private const string ResourcePrefix = "SaasBuilder.Cli.Templates.Module";

    internal static async Task ScaffoldAsync(string moduleName, string solutionRoot)
    {
        ValidateName(moduleName);

        var moduleRoot = Path.Combine(solutionRoot, "src", "Modules", moduleName);
        if (Directory.Exists(moduleRoot))
        {
            Console.Error.WriteLine($"Module directory already exists: {moduleRoot}");
            Environment.Exit(1);
        }

        Console.WriteLine($"Scaffolding module: {moduleName}");
        Console.WriteLine($"  Target: {moduleRoot}");

        var replacements = BuildReplacements(moduleName);

        var projects = new[] { "Contracts", "Domain", "Application", "Infrastructure", "Api" };
        foreach (var project in projects)
        {
            var projectDir = Path.Combine(moduleRoot, project);
            Directory.CreateDirectory(projectDir);
            Console.WriteLine($"  Created: {project}/");

            var prefix = $"{ResourcePrefix}.{project}";
            var resources = TemplateEngine.ListResources(prefix);

            foreach (var resource in resources)
            {
                var content = TemplateEngine.Render(resource, replacements);

                // Map resource name to a file path relative to project dir
                var fileName = ResourceNameToFileName(resource, prefix, moduleName);
                var filePath = Path.Combine(projectDir, fileName);

                // Ensure subdirectory exists
                var dir = Path.GetDirectoryName(filePath);
                if (dir is not null)
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);
                Console.WriteLine($"    + {Path.GetRelativePath(moduleRoot, filePath)}");
            }
        }

        // Emit an RLS migration template
        var migrationDir = Path.Combine(moduleRoot, "Infrastructure", "Migrations");
        Directory.CreateDirectory(migrationDir);
        var rlsContent = BuildRlsMigration(moduleName);
        var rlsPath = Path.Combine(migrationDir, $"001_initial_{moduleName.ToLowerInvariant()}_rls.sql");
        await File.WriteAllTextAsync(rlsPath, rlsContent).ConfigureAwait(false);
        Console.WriteLine($"    + Infrastructure/Migrations/{Path.GetFileName(rlsPath)}");

        // Emit integration test stub
        var testDir = Path.Combine(solutionRoot, "tests", $"{moduleName}.Tests");
        Directory.CreateDirectory(testDir);
        await EmitTestStubAsync(moduleName, testDir, replacements).ConfigureAwait(false);

        Console.WriteLine($"Module '{moduleName}' scaffolded. Next steps:");
        Console.WriteLine($"  1. Add projects to SaasBuilder.sln");
        Console.WriteLine($"  2. Register {moduleName}ModuleStartup in your host Program.cs");
        Console.WriteLine($"  3. Run: dotnet build");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsUpper(name[0]) || name.Any(c => !char.IsLetterOrDigit(c)))
        {
            Console.Error.WriteLine("Module name must be PascalCase (e.g. Inventory, CustomerPortal).");
            Environment.Exit(1);
        }
    }

    private static IReadOnlyDictionary<string, string> BuildReplacements(string moduleName) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__ModuleName__"] = moduleName,
            ["__module_schema__"] = moduleName.ToLowerInvariant(),
            ["__module_route__"] = moduleName.ToLowerInvariant(),
        };

    private static string ResourceNameToFileName(string resource, string prefix, string moduleName)
    {
        // Resource: SaasBuilder.Cli.Templates.Module.Api.__ModuleName__ModuleStartup.cs
        // Strip prefix + dot to get: __ModuleName__ModuleStartup.cs
        var relative = resource[(prefix.Length + 1)..];

        // Replace embedded placeholder in filename
        relative = relative.Replace("__ModuleName__", moduleName, StringComparison.Ordinal);

        // Resource names use '.' as separator — last segment is file extension
        // e.g. "SaasBuilder.Cli.Templates.Module.Infrastructure.__ModuleName__DbContext.cs"
        // After stripping prefix: "__ModuleName__DbContext.cs" — no dots in it except extension
        // So we can return it as-is (the extension is the last dot-segment)
        return relative;
    }

    private static string BuildRlsMigration(string moduleName) =>
        $"""
        -- RLS policy template for module: {moduleName}
        -- Replace <table_name> with actual table names.

        ALTER TABLE {moduleName.ToLowerInvariant()}.<table_name> ENABLE ROW LEVEL SECURITY;
        ALTER TABLE {moduleName.ToLowerInvariant()}.<table_name> FORCE ROW LEVEL SECURITY;

        DROP POLICY IF EXISTS tenant_isolation ON {moduleName.ToLowerInvariant()}.<table_name>;
        CREATE POLICY tenant_isolation
            ON {moduleName.ToLowerInvariant()}.<table_name>
            USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
        """;

    private static async Task EmitTestStubAsync(
        string moduleName,
        string testDir,
        IReadOnlyDictionary<string, string> replacements)
    {
        var csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit" />
                <PackageReference Include="xunit.runner.visualstudio" />
                <PackageReference Include="Microsoft.NET.Test.Sdk" />
                <PackageReference Include="FluentAssertions" />
                <PackageReference Include="Moq" />
              </ItemGroup>
            </Project>
            """;

        await File.WriteAllTextAsync(Path.Combine(testDir, $"{moduleName}.Tests.csproj"), csproj)
            .ConfigureAwait(false);

        var tenantLeakTest = $$"""
            using FluentAssertions;
            using Xunit;

            namespace {{moduleName}}.Tests;

            /// <summary>
            /// Tenant isolation test — proves tenant A cannot read tenant B data in {{moduleName}}.
            /// Replace the stub with a real WebApplicationFactory + TestContainers test.
            /// </summary>
            public sealed class TenantLeakTests
            {
                [Fact]
                public void TenantIsolation_{{moduleName}}_StubTest()
                {
                    // TODO: replace with real integration test using TestContainers + two tenant tokens
                    // See docs/add-a-custom-module.md for the pattern
                    true.Should().BeTrue("stub — implement real tenant isolation test");
                }
            }
            """;

        await File.WriteAllTextAsync(Path.Combine(testDir, "TenantLeakTests.cs"), tenantLeakTest)
            .ConfigureAwait(false);

        Console.WriteLine($"  Created: tests/{moduleName}.Tests/");
    }
}
