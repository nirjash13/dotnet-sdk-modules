using System.CommandLine;
using System.CommandLine.Parsing;
using SaasBuilder.Cli.Scaffolding;

namespace SaasBuilder.Cli.Commands;

/// <summary>
/// <c>saas add module &lt;name&gt;</c> — scaffolds a new module under <c>src/Modules/&lt;name&gt;/</c>
/// with five projects: Contracts, Domain, Application, Infrastructure, Api.
/// </summary>
internal static class AddModuleCommand
{
    internal static Command Create()
    {
        var nameArg = new Argument<string>("name") { Description = "Module name (PascalCase, e.g. Inventory)" };

        var outputOpt = new Option<string>("--output")
        {
            DefaultValueFactory = _ => string.Empty,
            Description = "Solution root directory. Defaults to current directory.",
        };

        var command = new Command("module", "Scaffold a new vertical-slice module with 5 projects");
        command.Add(nameArg);
        command.Add(outputOpt);

        command.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var name = pr.GetValue(nameArg)!;
            var output = pr.GetValue(outputOpt) ?? string.Empty;

            var root = string.IsNullOrWhiteSpace(output)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(output);

            await ModuleScaffolder.ScaffoldAsync(name, root).ConfigureAwait(false);
        });

        return command;
    }
}
