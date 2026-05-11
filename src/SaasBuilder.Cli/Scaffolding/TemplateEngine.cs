using System.Reflection;

namespace SaasBuilder.Cli.Scaffolding;

/// <summary>
/// Reads embedded resource templates, replaces placeholders, and writes output files.
/// </summary>
internal static class TemplateEngine
{
    private static readonly Assembly ThisAssembly = typeof(TemplateEngine).Assembly;

    /// <summary>
    /// Returns the content of an embedded resource, with all replacements applied.
    /// </summary>
    internal static string Render(string resourcePath, IReadOnlyDictionary<string, string> replacements)
    {
        var content = ReadResource(resourcePath);
        foreach (var (key, value) in replacements)
        {
            content = content.Replace(key, value, StringComparison.Ordinal);
        }

        return content;
    }

    /// <summary>
    /// Lists all embedded resource names under the given prefix (using '.' as separator).
    /// </summary>
    internal static IReadOnlyList<string> ListResources(string prefix)
    {
        return ThisAssembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
    }

    private static string ReadResource(string resourceName)
    {
        using var stream = ThisAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
