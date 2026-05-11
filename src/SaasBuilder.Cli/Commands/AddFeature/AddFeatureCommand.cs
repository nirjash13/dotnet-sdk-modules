using System.CommandLine;
using System.CommandLine.Parsing;
using SaasBuilder.Cli.Scaffolding;

namespace SaasBuilder.Cli.Commands;

/// <summary>
/// <c>saas add feature &lt;module&gt; &lt;name&gt;</c> — scaffolds an endpoint + DTO + validator + handler
/// + integration test stub inside an existing module.
/// </summary>
internal static class AddFeatureCommand
{
    internal static Command Create()
    {
        var moduleArg = new Argument<string>("module") { Description = "Target module name (e.g. Inventory)" };
        var nameArg = new Argument<string>("name") { Description = "Feature name (PascalCase, e.g. CreateItem)" };

        var outputOpt = new Option<string>("--output")
        {
            DefaultValueFactory = _ => string.Empty,
            Description = "Solution root directory. Defaults to current directory.",
        };

        var command = new Command("feature", "Scaffold a feature (endpoint + handler + validator + test) in a module");
        command.Add(moduleArg);
        command.Add(nameArg);
        command.Add(outputOpt);

        command.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var module = pr.GetValue(moduleArg)!;
            var name = pr.GetValue(nameArg)!;
            var output = pr.GetValue(outputOpt) ?? string.Empty;

            var root = string.IsNullOrWhiteSpace(output)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(output);

            await FeatureScaffolder.ScaffoldAsync(module, name, root).ConfigureAwait(false);
        });

        return command;
    }
}
