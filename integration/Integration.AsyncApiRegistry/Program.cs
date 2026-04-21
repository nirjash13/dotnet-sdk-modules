using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables();

var app = builder.Build();

app.UseStaticFiles();

// GET /schemas — list all (module, event, version) tuples by scanning wwwroot/schemas/
app.MapGet("/schemas", (IWebHostEnvironment env) =>
{
    var schemasRoot = Path.Combine(env.WebRootPath, "schemas");
    if (!Directory.Exists(schemasRoot))
    {
        return Results.Ok(Array.Empty<object>());
    }

    var results = new List<object>();

    // Expected layout: wwwroot/schemas/{module}/{event}/{version}.asyncapi.json
    foreach (var moduleDir in Directory.EnumerateDirectories(schemasRoot))
    {
        var module = Path.GetFileName(moduleDir);
        foreach (var eventDir in Directory.EnumerateDirectories(moduleDir))
        {
            var eventName = Path.GetFileName(eventDir);
            foreach (var schemaFile in Directory.EnumerateFiles(eventDir, "*.asyncapi.json"))
            {
                var fileName = Path.GetFileName(schemaFile);
                // Strip ".asyncapi.json" suffix to get the version.
                var version = fileName.Replace(".asyncapi.json", string.Empty, StringComparison.OrdinalIgnoreCase);
                results.Add(new { module, @event = eventName, version });
            }
        }
    }

    return Results.Ok(results);
});

// GET /schemas/{module}/{event}/{version} — serve the AsyncAPI document
app.MapGet("/schemas/{module}/{event}/{version}", (
    string module,
    string @event,
    string version,
    IWebHostEnvironment env) =>
{
    // Sanitize path segments to prevent directory traversal.
    if (!IsValidSegment(module) || !IsValidSegment(@event) || !IsValidSegment(version))
    {
        return Results.Problem(
            detail: "Invalid path segment.",
            statusCode: 400,
            title: "Bad Request");
    }

    var filePath = Path.Combine(
        env.WebRootPath,
        "schemas",
        module,
        @event,
        $"{version}.asyncapi.json");

    if (!File.Exists(filePath))
    {
        return Results.Problem(
            detail: $"Schema '{module}/{@event}/{version}' not found.",
            statusCode: 404,
            title: "Not Found");
    }

    var content = File.ReadAllText(filePath);

    // Validate it is parseable JSON before serving (fail-fast on corrupt files).
    using var doc = JsonDocument.Parse(content);

    return Results.Content(content, "application/json");
});

app.Run();

// Path segment validator: only alphanumeric, hyphens, dots, and underscores.
static bool IsValidSegment(string segment) =>
    !string.IsNullOrWhiteSpace(segment) &&
    Regex.IsMatch(segment, @"^[a-zA-Z0-9\-_.]+$");

// Expose Program for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
