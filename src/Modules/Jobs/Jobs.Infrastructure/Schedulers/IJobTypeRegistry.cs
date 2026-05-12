using System;
using System.Collections.Generic;
using Jobs.Application.Abstractions;

namespace Jobs.Infrastructure.Schedulers;

/// <summary>
/// Allowlist of job types that <see cref="HangfireJobDispatcher"/> is permitted to deserialize
/// and dispatch. Prevents arbitrary CLR-type instantiation from untrusted data in the Hangfire
/// job tables (reflection-based deserialization security boundary — C-4).
/// </summary>
public interface IJobTypeRegistry
{
    /// <summary>
    /// Registers <typeparamref name="T"/> as a permitted job type.
    /// Must be called once per concrete job type at application startup.
    /// </summary>
    void Register<T>()
        where T : IJob;

    /// <summary>
    /// Resolves a <see cref="Type"/> by its assembly-qualified name only if it has been
    /// previously registered via <see cref="Register{T}"/>. Returns <see langword="null"/>
    /// when the name is unknown, preventing deserialization of unregistered types.
    /// </summary>
    Type? Resolve(string assemblyQualifiedName);
}

/// <summary>Default in-process implementation backed by a hash-set of registered types.</summary>
internal sealed class JobTypeRegistry : IJobTypeRegistry
{
    private readonly HashSet<Type> _allowedTypes = new HashSet<Type>();

    /// <inheritdoc />
    public void Register<T>()
        where T : IJob
        => _allowedTypes.Add(typeof(T));

    /// <inheritdoc />
    public Type? Resolve(string assemblyQualifiedName)
    {
        if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
        {
            return null;
        }

        // Use Type.GetType to resolve the candidate, then check it is in the allowlist.
        // We accept the full assembly-qualified name stored by HangfireJobScheduler.
        Type? candidate = Type.GetType(assemblyQualifiedName);
        return candidate is not null && _allowedTypes.Contains(candidate) ? candidate : null;
    }
}
