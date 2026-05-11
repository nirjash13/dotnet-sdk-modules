using System.CommandLine;

namespace SaasBuilder.Cli.Commands;

/// <summary>
/// <c>saas add</c> — parent command for module and feature scaffolding.
/// </summary>
internal static class AddCommand
{
    internal static Command Create()
    {
        var command = new Command("add", "Add a module or feature to the current solution");
        command.Add(AddModuleCommand.Create());
        command.Add(AddFeatureCommand.Create());
        return command;
    }
}
