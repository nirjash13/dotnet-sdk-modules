using System.CommandLine;
using System.CommandLine.Parsing;

namespace SaasBuilder.Cli.Commands;

/// <summary>
/// <c>saas pack</c> — wraps <c>dotnet pack -c Release</c> over all packable SDK projects
/// with a single MinVer-derived version.
/// </summary>
internal static class PackCommand
{
    private static readonly string[] PackableProjects =
    [
        "src/SaasBuilder.SharedKernel/SaasBuilder.SharedKernel.csproj",
        "src/SaasBuilder.Persistence/SaasBuilder.Persistence.csproj",
        "src/SaasBuilder.Host/SaasBuilder.Host.csproj",
        "src/SaasBuilder.Tenancy/SaasBuilder.Tenancy.csproj",
        "src/SaasBuilder.Cli/SaasBuilder.Cli.csproj",
        "templates/SaasBuilder.Templates/SaasBuilder.Templates.csproj",
    ];

    internal static Command Create()
    {
        var rootOpt = new Option<string>("--root")
        {
            DefaultValueFactory = _ => string.Empty,
            Description = "Solution root directory. Defaults to current directory.",
        };

        var outputOpt = new Option<string>("--output")
        {
            DefaultValueFactory = _ => "artifacts",
            Description = "Output directory for .nupkg files. Defaults to 'artifacts'.",
        };

        var command = new Command("pack", "Build all SDK packages with consistent MinVer version");
        command.Add(rootOpt);
        command.Add(outputOpt);

        command.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var root = pr.GetValue(rootOpt) ?? string.Empty;
            var output = pr.GetValue(outputOpt) ?? "artifacts";

            var solutionRoot = string.IsNullOrWhiteSpace(root)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(root);

            var outputPath = Path.IsPathRooted(output)
                ? output
                : Path.Combine(solutionRoot, output);

            Directory.CreateDirectory(outputPath);
            Console.WriteLine($"Packing SDK projects to: {outputPath}");

            var anyFailed = false;
            foreach (var project in PackableProjects)
            {
                var projectPath = Path.Combine(
                    solutionRoot,
                    project.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(projectPath))
                {
                    Console.WriteLine($"  Skipped (not found): {project}");
                    continue;
                }

                Console.WriteLine($"  Packing: {project}");
                var exitCode = await NewCommand.RunProcessAsync(
                    "dotnet",
                    $"pack \"{projectPath}\" -c Release -o \"{outputPath}\" --no-restore",
                    solutionRoot)
                    .ConfigureAwait(false);

                if (exitCode != 0)
                {
                    Console.Error.WriteLine($"  ERROR: pack failed for {project} (exit {exitCode})");
                    anyFailed = true;
                }
            }

            if (anyFailed)
            {
                Environment.Exit(1);
            }

            Console.WriteLine($"All packages written to {outputPath}");
        });

        return command;
    }
}
