using System.CommandLine;
using System.CommandLine.Parsing;
using SaasBuilder.Cli.Commands;

var rootCommand = new RootCommand("SaaS Builder CLI — scaffold apps, modules, features, and manage tenants");

rootCommand.Add(NewCommand.Create());
rootCommand.Add(AddCommand.Create());
rootCommand.Add(MigrateCommand.Create());
rootCommand.Add(TenantCommand.Create());
rootCommand.Add(PackCommand.Create());
rootCommand.Add(DoctorCommand.Create());

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
