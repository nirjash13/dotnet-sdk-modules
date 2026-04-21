using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Chassis.Host.Observability;
using Chassis.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chassis.Host.Modules;

/// <summary>
/// Discovers <see cref="IModuleStartup"/> implementations by scanning assemblies in
/// <c>AppDomain.CurrentDomain.BaseDirectory</c> (and the optional <c>modules/</c> sub-directory).
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
    private List<ModuleLoadDiagnostic> _diagnostics = new List<ModuleLoadDiagnostic>();
    private List<IModuleStartup>? _cachedModules;

    public ReflectionModuleLoader(ILogger<ReflectionModuleLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                TryLoadFromAssembly(dllPath, seen, modules);
            }
        }

        _cachedModules = modules;
        return modules;
    }

    private void TryLoadFromAssembly(
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

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Partial type load — iterate what we got; filter out null entries.
            // ex.Types is Type?[] (nullable elements); we build a non-nullable Type[].
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
            ChassisMeters.ModuleLoadDuration.Record(durationSeconds, tags);

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
