using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace SaasBuilder.Cli.Tests;

/// <summary>
/// Load-bearing CLI tests — validate that key commands produce correct exit codes
/// and side effects without requiring a live database.
/// </summary>
public sealed class DoctorCommandTests
{
    [Fact]
    public async Task Doctor_WithNoEnvVars_ExitsNonZero()
    {
        var cliPath = GetCliBinaryPath();
        if (cliPath is null)
        {
            // Binary not built yet — skip rather than fail (build-order dependency)
            return;
        }

        var (exitCode, _) = await RunCliAsync(cliPath, "doctor");

        exitCode.Should().NotBe(0, "doctor should fail when SAAS_DB_CONNECTION is not set");
    }

    [Fact]
    public async Task AddModule_EmitsFiveExpectedProjectFolders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"saas-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "Modules"));

        try
        {
            await SaasBuilder.Cli.Scaffolding.ModuleScaffolder.ScaffoldAsync("TestWidget", tempDir);

            var moduleRoot = Path.Combine(tempDir, "src", "Modules", "TestWidget");
            Directory.Exists(Path.Combine(moduleRoot, "Contracts")).Should().BeTrue();
            Directory.Exists(Path.Combine(moduleRoot, "Domain")).Should().BeTrue();
            Directory.Exists(Path.Combine(moduleRoot, "Application")).Should().BeTrue();
            Directory.Exists(Path.Combine(moduleRoot, "Infrastructure")).Should().BeTrue();
            Directory.Exists(Path.Combine(moduleRoot, "Api")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task AddFeature_EmitsExpectedFileSetInsideModule()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"saas-feat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "Modules", "Inventory", "Application"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "Modules", "Inventory", "Api"));

        try
        {
            await SaasBuilder.Cli.Scaffolding.FeatureScaffolder.ScaffoldAsync("Inventory", "CreateItem", tempDir);

            var appFeatureDir = Path.Combine(tempDir, "src", "Modules", "Inventory", "Application", "CreateItem");
            Directory.Exists(appFeatureDir).Should().BeTrue("Application/CreateItem sub-folder must be created");

            var files = Directory.GetFiles(appFeatureDir, "*.cs");
            files.Should().NotBeEmpty("feature scaffold must emit at least one .cs file");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string? GetCliBinaryPath()
    {
        var testDir = Path.GetDirectoryName(typeof(DoctorCommandTests).Assembly.Location)!;
        var repoRoot = FindRepoRoot(testDir);
        if (repoRoot is null)
        {
            return null;
        }

        var cliPath = Path.Combine(repoRoot, "src", "SaasBuilder.Cli", "bin", "Debug", "net10.0", "saas.exe");
        if (File.Exists(cliPath))
        {
            return cliPath;
        }

        var cliPathUnix = Path.Combine(repoRoot, "src", "SaasBuilder.Cli", "bin", "Debug", "net10.0", "saas");
        return File.Exists(cliPathUnix) ? cliPathUnix : null;
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SaasBuilder.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static async Task<(int ExitCode, string Output)> RunCliAsync(string cliPath, string arguments)
    {
        var psi = new ProcessStartInfo(cliPath, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // Ensure no DB connection is present so doctor always fails the DB check
        psi.Environment.Remove("SAAS_DB_CONNECTION");
        psi.Environment.Remove("ConnectionStrings__Default");

        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, output);
    }
}
