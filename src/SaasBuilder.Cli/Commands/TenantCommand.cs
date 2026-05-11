using System.CommandLine;
using System.CommandLine.Parsing;
using Npgsql;

namespace SaasBuilder.Cli.Commands;

/// <summary>
/// <c>saas tenant create &lt;slug&gt;</c> — provisions a tenant row in the <c>tenants</c> table
/// and seeds the Owner role.
/// </summary>
internal static class TenantCommand
{
    internal static Command Create()
    {
        var command = new Command("tenant", "Manage tenants");
        command.Add(CreateTenantSubCommand());
        return command;
    }

    private static Command CreateTenantSubCommand()
    {
        var slugArg = new Argument<string>("slug")
        {
            Description = "Tenant slug (lowercase, URL-safe, e.g. acme-corp)",
        };

        var connOpt = new Option<string>("--connection")
        {
            DefaultValueFactory = _ => string.Empty,
            Description = "Postgres connection string. Falls back to SAAS_DB_CONNECTION env var or appsettings.Development.json.",
        };

        var command = new Command("create", "Provision a new tenant row and seed Owner role");
        command.Add(slugArg);
        command.Add(connOpt);

        command.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var slug = pr.GetValue(slugArg)!;
            var conn = pr.GetValue(connOpt) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(slug) || !IsValidSlug(slug))
            {
                Console.Error.WriteLine("Slug must be lowercase alphanumeric with hyphens (e.g. acme-corp).");
                Environment.Exit(1);
                return;
            }

            var connectionString = ResolveConnectionString(conn);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine(
                    "No connection string found. Set SAAS_DB_CONNECTION env var or pass --connection.");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Creating tenant: {slug}");

            try
            {
                await using var npgsql = new NpgsqlConnection(connectionString);
                await npgsql.OpenAsync(ct).ConfigureAwait(false);

                await using var tx = await npgsql.BeginTransactionAsync(ct).ConfigureAwait(false);
                try
                {
                    var tenantId = Guid.NewGuid();
                    var now = DateTimeOffset.UtcNow;

                    await using (var cmd = npgsql.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = """
                            INSERT INTO public.tenants (id, slug, status, created_at)
                            VALUES (@id, @slug, 'Active', @now)
                            ON CONFLICT (slug) DO NOTHING
                            RETURNING id;
                            """;
                        cmd.Parameters.AddWithValue("id", tenantId);
                        cmd.Parameters.AddWithValue("slug", slug);
                        cmd.Parameters.AddWithValue("now", now);

                        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                        if (result is null)
                        {
                            Console.Error.WriteLine($"Tenant '{slug}' already exists.");
                            await tx.RollbackAsync(ct).ConfigureAwait(false);
                            Environment.Exit(2);
                            return;
                        }
                    }

                    await using (var cmd = npgsql.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = """
                            INSERT INTO public.roles (id, tenant_id, name, is_system)
                            VALUES (@id, @tenantId, 'Owner', true)
                            ON CONFLICT DO NOTHING;
                            """;
                        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
                        cmd.Parameters.AddWithValue("tenantId", tenantId);
                        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    Console.WriteLine($"Tenant '{slug}' created with id={tenantId}.");
                }
                catch
                {
                    await tx.RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"Failed to create tenant: {ex.Message}");
                Environment.Exit(1);
            }
        });

        return command;
    }

    private static bool IsValidSlug(string slug) =>
        slug.Length >= 2 &&
        slug.Length <= 63 &&
        slug.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-') &&
        !slug.StartsWith('-') &&
        !slug.EndsWith('-');

    private static string ResolveConnectionString(string explicitValue)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }

        var env = Environment.GetEnvironmentVariable("SAAS_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var appsettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json");
        if (!File.Exists(appsettingsPath))
        {
            return string.Empty;
        }

        try
        {
            var json = File.ReadAllText(appsettingsPath);
            const string key = "\"ConnectionStrings\"";
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0)
            {
                return string.Empty;
            }

            var defaultIdx = json.IndexOf("\"Default\"", idx, StringComparison.Ordinal);
            if (defaultIdx < 0)
            {
                return string.Empty;
            }

            var colonIdx = json.IndexOf(':', defaultIdx);
            var quoteStart = json.IndexOf('"', colonIdx + 1);
            var quoteEnd = json.IndexOf('"', quoteStart + 1);
            return quoteStart >= 0 && quoteEnd > quoteStart
                ? json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1)
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
