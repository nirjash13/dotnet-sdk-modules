using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;

namespace SaasBuilder.Cli.Commands;

/// <summary>
/// <c>saas new &lt;name&gt;</c> — wraps <c>dotnet new saas-api -n &lt;name&gt;</c>.
/// </summary>
internal static class NewCommand
{
    internal static Command Create()
    {
        var nameArg = new Argument<string>("name") { Description = "Name of the new SaaS application" };

        var isolationOpt = new Option<string>("--isolation")
        {
            DefaultValueFactory = _ => string.Empty,
            Description = "Tenant isolation mode: pool-rls | silo-schema | silo-db | silo-stamp",
        };

        var command = new Command("new", "Scaffold a new SaaS application via dotnet new saas-api");
        command.Add(nameArg);
        command.Add(isolationOpt);

        command.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var name = pr.GetValue(nameArg)!;
            var isolation = pr.GetValue(isolationOpt) ?? string.Empty;

            Console.WriteLine($"Scaffolding new SaaS app: {name}");

            var dotnetArgs = $"new saas-api -n {name}";
            if (!string.IsNullOrWhiteSpace(isolation))
            {
                dotnetArgs += $" --isolation {isolation}";
            }

            var exitCode = await RunProcessAsync("dotnet", dotnetArgs).ConfigureAwait(false);
            if (exitCode != 0)
            {
                Console.Error.WriteLine($"dotnet new saas-api failed with exit code {exitCode}.");
                Console.Error.WriteLine("Ensure the SaasBuilder.Templates package is installed:");
                Console.Error.WriteLine("  dotnet new install SaasBuilder.Templates");
                Environment.Exit(exitCode);
            }

            Console.WriteLine($"Created {name}. Run: cd {name} && docker compose up -d && dotnet run");
        });

        return command;
    }

    internal static async Task<int> RunProcessAsync(string fileName, string arguments, string? workingDir = null)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory(),
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }
}
