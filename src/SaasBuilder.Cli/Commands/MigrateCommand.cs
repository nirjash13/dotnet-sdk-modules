using System.CommandLine;
using System.CommandLine.Parsing;

namespace SaasBuilder.Cli.Commands;

/// <summary>
/// <c>saas migrate</c> — runs <c>dotnet ef database update</c> across all module projects
/// that contain EF Core migrations, in dependency order.
/// </summary>
internal static class MigrateCommand
{
    private static readonly string[] ModuleOrder =
    [
        "Identity",
        "Billing",
        "Entitlements",
        "FeatureFlags",
        "Notifications",
        "Files",
        "Jobs",
        "Audit",
        "Webhooks",
        "Search",
        "Realtime",
        "Admin",
        "Gdpr",
        "Ledger",
        "Reporting",
        "Registration",
    ];

    internal static Command Create()
    {
        var rootOpt = new Option<string>("--root")
        {
            DefaultValueFactory = _ => string.Empty,
            Description = "Solution root directory. Defaults to current directory.",
        };

        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Print migration commands without executing them.",
        };

        var command = new Command("migrate", "Apply pending EF Core migrations across all modules");
        command.Add(rootOpt);
        command.Add(dryRunOpt);

        command.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var root = pr.GetValue(rootOpt) ?? string.Empty;
            var dryRun = pr.GetValue(dryRunOpt);

            var solutionRoot = string.IsNullOrWhiteSpace(root)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(root);

            Console.WriteLine($"Running migrations from: {solutionRoot}");
            if (dryRun)
            {
                Console.WriteLine("(dry-run — no changes will be applied)");
            }

            var anyFailed = false;
            foreach (var module in ModuleOrder)
            {
                var infraPath = Path.Combine(
                    solutionRoot, "src", "Modules", module, $"{module}.Infrastructure");

                if (!Directory.Exists(infraPath))
                {
                    continue;
                }

                var migrationsPath = Path.Combine(infraPath, "Migrations");
                if (!Directory.Exists(migrationsPath))
                {
                    continue;
                }

                Console.WriteLine($"  Migrating: {module}");

                if (!dryRun)
                {
                    var exitCode = await NewCommand.RunProcessAsync(
                        "dotnet",
                        $"ef database update --project \"{infraPath}\"")
                        .ConfigureAwait(false);

                    if (exitCode != 0)
                    {
                        Console.Error.WriteLine($"  ERROR: migration failed for {module} (exit {exitCode})");
                        anyFailed = true;
                    }
                }
            }

            if (anyFailed)
            {
                Environment.Exit(1);
            }

            Console.WriteLine("Migrations complete.");
        });

        return command;
    }
}
