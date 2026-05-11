namespace SaasBuilder.Cli.Scaffolding;

/// <summary>
/// Scaffolds a new feature inside an existing module.
/// Emits: Request DTO, Response DTO, Validator, Handler, Endpoint, Integration test stub.
/// </summary>
internal static class FeatureScaffolder
{
    private const string ResourcePrefix = "SaasBuilder.Cli.Templates.Feature";

    internal static async Task ScaffoldAsync(string moduleName, string featureName, string solutionRoot)
    {
        ValidateNames(moduleName, featureName);

        var moduleRoot = Path.Combine(solutionRoot, "src", "Modules", moduleName);
        if (!Directory.Exists(moduleRoot))
        {
            Console.Error.WriteLine($"Module not found: {moduleRoot}");
            Console.Error.WriteLine($"Run 'saas add module {moduleName}' first.");
            Environment.Exit(1);
        }

        Console.WriteLine($"Scaffolding feature: {featureName} in module {moduleName}");

        var replacements = BuildReplacements(moduleName, featureName);

        // Application layer: Request/Response/Validator/Handler in a sub-folder
        var appFeatureDir = Path.Combine(moduleRoot, "Application", featureName);
        Directory.CreateDirectory(appFeatureDir);

        // Api layer: Endpoint file
        var apiDir = Path.Combine(moduleRoot, "Api");
        Directory.CreateDirectory(apiDir);

        var resources = TemplateEngine.ListResources(ResourcePrefix);

        foreach (var resource in resources)
        {
            var content = TemplateEngine.Render(resource, replacements);
            var (targetDir, fileName) = ResolveTarget(resource, moduleName, featureName, appFeatureDir, apiDir);

            var filePath = Path.Combine(targetDir, fileName);
            if (File.Exists(filePath))
            {
                Console.WriteLine($"  Skipped (exists): {filePath}");
                continue;
            }

            await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);
            Console.WriteLine($"  + {Path.GetRelativePath(solutionRoot, filePath)}");
        }

        // Integration test stub
        var testDir = Path.Combine(solutionRoot, "tests", $"{moduleName}.Tests");
        Directory.CreateDirectory(testDir);
        await EmitIntegrationTestAsync(moduleName, featureName, testDir, replacements).ConfigureAwait(false);

        Console.WriteLine($"Feature '{featureName}' scaffolded in module '{moduleName}'. Run: dotnet build");
    }

    private static void ValidateNames(string module, string feature)
    {
        if (string.IsNullOrWhiteSpace(module) || !char.IsUpper(module[0]))
        {
            Console.Error.WriteLine("Module name must be PascalCase.");
            Environment.Exit(1);
        }

        if (string.IsNullOrWhiteSpace(feature) || !char.IsUpper(feature[0]))
        {
            Console.Error.WriteLine("Feature name must be PascalCase.");
            Environment.Exit(1);
        }
    }

    private static IReadOnlyDictionary<string, string> BuildReplacements(string moduleName, string featureName) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__ModuleName__"] = moduleName,
            ["__FeatureName__"] = featureName,
            ["__module_route__"] = moduleName.ToLowerInvariant(),
            ["__feature_route__"] = ToKebab(featureName),
        };

    private static (string TargetDir, string FileName) ResolveTarget(
        string resource,
        string moduleName,
        string featureName,
        string appFeatureDir,
        string apiDir)
    {
        var localName = resource[(ResourcePrefix.Length + 1)..];
        localName = localName
            .Replace("__FeatureName__", featureName, StringComparison.Ordinal)
            .Replace("__ModuleName__", moduleName, StringComparison.Ordinal);

        if (localName.Contains("Endpoint", StringComparison.Ordinal))
        {
            return (apiDir, localName);
        }

        // Tests file goes to the test stub location — not here; handled separately
        if (localName.Contains("Tests", StringComparison.Ordinal))
        {
            return (appFeatureDir, localName); // temp; overridden in caller
        }

        return (appFeatureDir, localName);
    }

    private static async Task EmitIntegrationTestAsync(
        string moduleName,
        string featureName,
        string testDir,
        IReadOnlyDictionary<string, string> replacements)
    {
        var fileName = $"{featureName}Tests.cs";
        var filePath = Path.Combine(testDir, fileName);

        if (File.Exists(filePath))
        {
            Console.WriteLine($"  Skipped (exists): {filePath}");
            return;
        }

        var content = $$"""
            using FluentAssertions;
            using Xunit;

            namespace {{moduleName}}.Tests;

            /// <summary>Integration tests for {{featureName}} in {{moduleName}}.</summary>
            public sealed class {{featureName}}Tests
            {
                [Fact]
                public async Task {{featureName}}_WhenRequestIsValid_ReturnsSuccess()
                {
                    // TODO: use WebApplicationFactory + TestContainers for real integration test
                    await Task.CompletedTask.ConfigureAwait(false);
                    true.Should().BeTrue("stub — implement with WebApplicationFactory");
                }
            }
            """;

        await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);
        Console.WriteLine($"  + {Path.GetRelativePath(testDir, filePath)}");
    }

    private static string ToKebab(string pascalCase) =>
        string.Concat(
            pascalCase.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? $"-{char.ToLowerInvariant(c)}" : char.ToLowerInvariant(c).ToString()))
        .TrimStart('-');
}
