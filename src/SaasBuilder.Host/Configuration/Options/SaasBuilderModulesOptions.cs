using System.Collections.Generic;
using System.Reflection;
using SaasBuilder.SharedKernel.Abstractions;

namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Options controlling how <see cref="IModuleStartup"/> implementations are discovered.
/// </summary>
/// <remarks>
/// <para>
/// Discovery sources are additive — all configured assemblies and probe directories are scanned.
/// When no sources are configured the loader falls back to the legacy behaviour:
/// scanning <c>AppDomain.CurrentDomain.BaseDirectory</c> and the <c>modules/</c> sub-directory.
/// </para>
/// </remarks>
public sealed class SaasBuilderModulesOptions
{
    private readonly List<Assembly> _assemblies = new List<Assembly>();
    private readonly List<string> _probeDirectories = new List<string>();

    /// <summary>Gets the assemblies explicitly registered for module scanning (read-only view).</summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies;

    /// <summary>Gets the directories to probe for module DLLs (read-only view).</summary>
    public IReadOnlyList<string> ProbeDirectories => _probeDirectories;

    /// <summary>
    /// Gets a value indicating whether any explicit sources have been configured.
    /// When <c>false</c> the loader uses the legacy base-directory scan.
    /// </summary>
    public bool HasExplicitSources => _assemblies.Count > 0 || _probeDirectories.Count > 0;

    /// <summary>
    /// Registers the assembly that contains <typeparamref name="T"/> for module scanning.
    /// Use this when the module is already compiled into the process (project reference or
    /// loaded assembly), and you want to discover its <see cref="IModuleStartup"/> without
    /// relying on the probe-directory DLL scan.
    /// </summary>
    public SaasBuilderModulesOptions ScanAssemblyContaining<T>()
    {
        Assembly assembly = typeof(T).Assembly;
        if (!_assemblies.Contains(assembly))
        {
            _assemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Registers the given assembly for module scanning.
    /// </summary>
    public SaasBuilderModulesOptions ScanAssembly(Assembly assembly)
    {
        if (!_assemblies.Contains(assembly))
        {
            _assemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Adds a directory path to be probed for module DLLs at startup.
    /// The loader enumerates <c>*.dll</c> files in the directory and loads each one,
    /// searching for types that implement <see cref="IModuleStartup"/>.
    /// </summary>
    /// <param name="path">
    /// Absolute or relative path to the probe directory.
    /// Relative paths are resolved from <c>AppDomain.CurrentDomain.BaseDirectory</c>.
    /// </param>
    public SaasBuilderModulesOptions AddProbeDirectory(string path)
    {
        if (!_probeDirectories.Contains(path))
        {
            _probeDirectories.Add(path);
        }

        return this;
    }

    /// <summary>
    /// Explicitly registers a module type for direct instantiation, bypassing DLL scanning.
    /// The type must have a public parameterless constructor.
    /// </summary>
    public SaasBuilderModulesOptions AddType<T>()
        where T : IModuleStartup, new()
    {
        return ScanAssemblyContaining<T>();
    }
}
