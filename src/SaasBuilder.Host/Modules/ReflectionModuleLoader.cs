using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using SaasBuilder.Host.Configuration.Options;
using SaasBuilder.Host.Observability;
using SaasBuilder.SharedKernel.Abstractions;

namespace SaasBuilder.Host.Modules;

/// <summary>
/// Discovers <see cref="IModuleStartup"/> implementations by scanning assemblies in
/// configured probe directories and/or explicitly registered assemblies.
/// Falls back to <c>AppDomain.CurrentDomain.BaseDirectory</c> (and the optional <c>modules/</c>
/// sub-directory) when no explicit sources are configured.
/// </summary>
/// <remarks>
/// <para>
/// Scan strategy: attempt to load every <c>*.dll</c> in the search paths; silently skip assemblies
/// that fail to load (native or otherwise unresolvable). For each loadable assembly, reflect over
/// public concrete types implementing <see cref="IModuleStartup"/> and instantiate them via
/// <see cref="Activator.CreateInstance(Type)"/> (parameterless constructor required).
/// </para>
/// <para>
/// Duplicate guard: if two assemblies export a type with the same <c>FullName</c>, only the first
/// discovered instance is kept and a warning is logged — this prevents accidental double-registration
/// when the same module DLL lands in both the base directory and the <c>modules/</c> sub-directory.
/// </para>
/// <para>
/// Metrics: emits a <c>chassis.module.load.duration</c> histogram sample per module.
/// </para>
/// </remarks>
internal sealed class ReflectionModuleLoader : IModuleLoader
{
    private readonly ILogger<ReflectionModuleLoader> _logger;
    private readonly SaasBuilderModulesOptions? _options;
    private List<ModuleLoadDiagnostic> _diagnostics = new List<ModuleLoadDiagnostic>();
    private List<IModuleStartup>? _cachedModules;

    /// <summary>
    /// Initialises the loader using legacy base-directory scanning behaviour.
    /// Scans <c>AppDomain.CurrentDomain.BaseDirectory</c> and the <c>modules/</c> sub-directory.
    /// </summary>
    public ReflectionModuleLoader(ILogger<ReflectionModuleLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = null;
    }

    /// <summary>
    /// Initialises the loader using the provided module options. When
    /// <see cref="SaasBuilderModulesOptions.HasExplicitSources"/> is <c>false</c> the loader
    /// falls back to the legacy base-directory scan.
    /// </summary>
    public ReflectionModuleLoader(
        ILogger<ReflectionModuleLoader> logger,
        SaasBuilderModulesOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public IReadOnlyList<ModuleLoadDiagnostic> LoadDiagnostics => _diagnostics;

    /// <inheritdoc />
    public IEnumerable<IModuleStartup> Load()
    {
        // Return cached modules if already loaded — prevents double-scan when Load() is
        // called multiple times (once for ConfigureServices during builder phase, once
        // for Configure during pipeline setup).
        if (_cachedModules != null)
        {
            return _cachedModules;
        }

        _diagnostics = new List<ModuleLoadDiagnostic>();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var modules = new List<IModuleStartup>();

        // ── Explicit assembly sources ──────────────────────────────────────────────
        if (_options is { HasExplicitSources: true })
        {
            foreach (Assembly assembly in _options.Assemblies)
            {
                ScanAssembly(assembly, seen, modules);
            }

            foreach (string probeDir in _options.ProbeDirectories)
            {
                string resolved = Path.IsPathRooted(probeDir)
                    ? probeDir
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, probeDir);

                if (!Directory.Exists(resolved))
                {
                    _logger.LogDebug(
                        "Module probe directory '{Dir}' does not exist — skipping.",
                        resolved);
                    continue;
                }

                foreach (string dllPath in Directory.EnumerateFiles(resolved, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    TryLoadFromDll(dllPath, seen, modules);
                }
            }
        }
        else
        {
            // ── Legacy fallback: scan base directory + optional modules/ subdir ──────
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var searchPaths = new List<string> { baseDir };

            string modulesSubDir = Path.Combine(baseDir, "modules");
            if (Directory.Exists(modulesSubDir))
            {
                searchPaths.Add(modulesSubDir);
            }

            foreach (string searchPath in searchPaths)
            {
                foreach (string dllPath in Directory.EnumerateFiles(searchPath, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    TryLoadFromDll(dllPath, seen, modules);
                }
            }
        }

        _cachedModules = modules;
        return modules;
    }

    private void ScanAssembly(
        Assembly assembly,
        HashSet<string> seen,
        List<IModuleStartup> modules)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var nonNullTypes = new List<Type>();
            if (ex.Types != null)
            {
                foreach (Type? t in ex.Types)
                {
                    if (t != null)
                    {
                        nonNullTypes.Add(t);
                    }
                }
            }

            types = nonNullTypes.ToArray();
        }

        InstantiateModulesFromTypes(types, assembly, seen, modules);
    }

    private void TryLoadFromDll(
        string dllPath,
        HashSet<string> seen,
        List<IModuleStartup> modules)
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(dllPath);
        }
        catch (Exception ex)
        {
            _logger.LogTrace("Skipped assembly {Path}: {Reason}", dllPath, ex.Message);
            return;
        }

        ScanAssembly(assembly, seen, modules);
    }

    private void InstantiateModulesFromTypes(
        Type[] types,
        Assembly assembly,
        HashSet<string> seen,
        List<IModuleStartup> modules)
    {
        Type moduleStartupType = typeof(IModuleStartup);
        foreach (Type type in types)
        {
            if (type.IsAbstract || type.IsInterface || !moduleStartupType.IsAssignableFrom(type))
            {
                continue;
            }

            string typeKey = type.FullName ?? type.Name;
            if (!seen.Add(typeKey))
            {
                _logger.LogWarning(
                    "Duplicate module type '{TypeName}' found in '{Assembly}' — skipped.",
                    typeKey,
                    assembly.GetName().Name);
                continue;
            }

            var sw = Stopwatch.StartNew();
            IModuleStartup? instance;
            try
            {
                instance = (IModuleStartup?)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to instantiate module '{TypeName}'.", typeKey);
                continue;
            }

            sw.Stop();

            if (instance is null)
            {
                continue;
            }

            double durationSeconds = sw.Elapsed.TotalSeconds;
            string assemblyName = assembly.GetName().Name ?? "unknown";

            var tags = new System.Diagnostics.TagList
            {
                { "module", typeKey },
            };
            SaasBuilderMeters.ModuleLoadDuration.Record(durationSeconds, tags);

            _diagnostics.Add(new ModuleLoadDiagnostic(typeKey, assemblyName, durationSeconds));
            modules.Add(instance);

            _logger.LogInformation(
                "Loaded module: {ModuleName} from {AssemblyName} in {DurationMs:F2}ms",
                typeKey,
                assemblyName,
                durationSeconds * 1000);
        }
    }
}
