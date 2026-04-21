using System.Collections.Generic;
using Chassis.SharedKernel.Abstractions;

namespace Chassis.Host.Modules;

/// <summary>
/// Discovers and returns all <see cref="IModuleStartup"/> instances available to this host.
/// </summary>
public interface IModuleLoader
{
    /// <summary>Returns diagnostic information captured during the last <see cref="Load"/> call.</summary>
    IReadOnlyList<ModuleLoadDiagnostic> LoadDiagnostics { get; }

    /// <summary>Discovers and returns all module startup instances.</summary>
    IEnumerable<IModuleStartup> Load();
}

/// <summary>
/// Diagnostic record captured per module during the load pass.
/// </summary>
/// <param name="moduleName">The fully-qualified type name of the <see cref="IModuleStartup"/> implementation.</param>
/// <param name="assemblyName">The assembly the module was discovered in.</param>
/// <param name="durationSeconds">Wall-clock seconds taken to instantiate the module.</param>
public sealed record ModuleLoadDiagnostic(
    string moduleName,
    string assemblyName,
    double durationSeconds);
