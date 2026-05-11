using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Net.Sockets;
using Npgsql;

namespace SaasBuilder.Cli.Commands;

/// <summary>
/// <c>saas doctor</c> — checks environment variables, pings the database, and reports
/// missing/configured providers in a pretty-printed table.
/// </summary>
internal static class DoctorCommand
{
    private sealed record CheckDef(string Name, string Category, Func<Task<CheckResult>> Run);

    private sealed record CheckResult(bool Ok, string Value, string? Hint = null);

    internal static Command Create()
    {
        var rootOpt = new Option<string>("--root")
        {
            DefaultValueFactory = _ => string.Empty,
            Description = "Solution root directory. Defaults to current directory.",
        };

        var command = new Command("doctor", "Diagnose environment configuration and connectivity");
        command.Add(rootOpt);

        command.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var root = pr.GetValue(rootOpt) ?? string.Empty;
            var solutionRoot = string.IsNullOrWhiteSpace(root)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(root);

            var checks = BuildChecks(solutionRoot);

            Console.WriteLine();
            Console.WriteLine("  saas doctor — environment check");
            Console.WriteLine(new string('-', 70));

            var anyFailed = false;
            foreach (var check in checks)
            {
                CheckResult result;
                try
                {
                    result = await check.Run().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    result = new CheckResult(false, "ERROR", ex.Message);
                }

                var icon = result.Ok ? "+" : "x";
                var status = result.Ok ? "OK  " : "FAIL";
                Console.WriteLine(
                    $"  [{icon}] {check.Category,-20} {check.Name,-30} {status} {result.Value}");

                if (!string.IsNullOrWhiteSpace(result.Hint))
                {
                    Console.WriteLine($"       hint: {result.Hint}");
                }

                if (!result.Ok)
                {
                    anyFailed = true;
                }
            }

            Console.WriteLine(new string('-', 70));
            Console.WriteLine(anyFailed
                ? "  Some checks failed. Fix the issues above and re-run 'saas doctor'."
                : "  All checks passed.");
            Console.WriteLine();

            if (anyFailed)
            {
                Environment.Exit(1);
            }
        });

        return command;
    }

    private static IReadOnlyList<CheckDef> BuildChecks(string solutionRoot)
    {
        return
        [
            new CheckDef("DB connection", "Database", async () =>
            {
                var conn = GetEnv("SAAS_DB_CONNECTION")
                    ?? GetEnv("ConnectionStrings__Default")
                    ?? TryReadAppsettingsConnection(solutionRoot);

                if (string.IsNullOrWhiteSpace(conn))
                {
                    return new CheckResult(
                        false,
                        "not set",
                        "Set SAAS_DB_CONNECTION or appsettings.Development.json ConnectionStrings.Default");
                }

                try
                {
                    await using var npgsql = new NpgsqlConnection(conn);
                    await npgsql.OpenAsync().ConfigureAwait(false);
                    return new CheckResult(true, "reachable");
                }
                catch (Exception ex)
                {
                    return new CheckResult(false, "unreachable", ex.Message);
                }
            }),

            new CheckDef("Identity:Authority", "Identity", () =>
                CheckEnv("Identity__Authority", "Required for OpenIddict OIDC issuer validation")),

            new CheckDef("Billing:Stripe:ApiKey", "Billing", () =>
                CheckEnv("Billing__Stripe__ApiKey", "Required for Stripe billing. Use sk_test_... for dev")),

            new CheckDef("Email provider", "Notifications", () =>
                CheckEnv(
                    "Notifications__Email__Provider",
                    "Set to: smtp | sendgrid | postmark | ses | mailgun. Defaults to smtp")),

            new CheckDef("BlobStore provider", "Files", () =>
                CheckEnv(
                    "Files__BlobStore__Provider",
                    "Set to: filesystem | azure | s3 | gcs. Defaults to filesystem")),

            new CheckDef("RabbitMQ host", "Transport", async () =>
            {
                var host = GetEnv("Dispatch__Bus__Host") ?? "localhost";
                const int port = 5672;
                try
                {
                    using var tcp = new TcpClient();
                    await tcp.ConnectAsync(host, port).ConfigureAwait(false);
                    return new CheckResult(true, $"{host}:{port} reachable");
                }
                catch
                {
                    return new CheckResult(
                        false,
                        $"{host}:{port} unreachable",
                        "Optional when using InProc transport (Dispatch:Transport=inproc)");
                }
            }),

            new CheckDef("OTel endpoint", "Observability", async () =>
            {
                var endpoint = GetEnv("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";
                try
                {
                    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                    {
                        return new CheckResult(false, "invalid URI", $"OTEL_EXPORTER_OTLP_ENDPOINT={endpoint}");
                    }

                    using var tcp = new TcpClient();
                    await tcp.ConnectAsync(uri.Host, uri.Port).ConfigureAwait(false);
                    return new CheckResult(true, $"{endpoint} reachable");
                }
                catch
                {
                    return new CheckResult(
                        false,
                        "unreachable",
                        "OTel collector is optional — tracing will be no-op if unavailable");
                }
            }),

            new CheckDef("FeatureFlags provider", "FeatureFlags", () =>
                CheckEnv(
                    "FeatureFlags__Provider",
                    "Set to: db | launchdarkly | unleash | flagsmith. Defaults to db")),

            new CheckDef(".NET SDK", "Tooling", async () =>
            {
                var (exitCode, version) = await RunAndCaptureAsync("dotnet", "--version").ConfigureAwait(false);
                return exitCode == 0
                    ? new CheckResult(true, version.Trim())
                    : new CheckResult(false, "not found", "Install .NET SDK 10+");
            }),
        ];
    }

    private static Task<CheckResult> CheckEnv(string envVar, string hint)
    {
        var value = GetEnv(envVar);
        return Task.FromResult(string.IsNullOrWhiteSpace(value)
            ? new CheckResult(false, "not set", hint)
            : new CheckResult(true, Mask(value)));
    }

    private static string? GetEnv(string key) =>
        Environment.GetEnvironmentVariable(key);

    private static string Mask(string value) =>
        value.Length <= 8 ? "***" : $"{value[..4]}...{value[^4..]}";

    private static string? TryReadAppsettingsConnection(string root)
    {
        var path = Path.Combine(root, "appsettings.Development.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            const string key = "\"ConnectionStrings\"";
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0)
            {
                return null;
            }

            var defaultIdx = json.IndexOf("\"Default\"", idx, StringComparison.Ordinal);
            if (defaultIdx < 0)
            {
                return null;
            }

            var colonIdx = json.IndexOf(':', defaultIdx);
            var quoteStart = json.IndexOf('"', colonIdx + 1);
            var quoteEnd = json.IndexOf('"', quoteStart + 1);
            return quoteStart >= 0 && quoteEnd > quoteStart
                ? json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAndCaptureAsync(string fileName, string argStr)
    {
        var psi = new ProcessStartInfo(fileName, argStr)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return (-1, string.Empty);
        }

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        return (process.ExitCode, output);
    }
}
