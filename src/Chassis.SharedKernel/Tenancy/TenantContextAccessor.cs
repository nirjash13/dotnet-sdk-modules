using System;
using System.Threading;

namespace Chassis.SharedKernel.Tenancy;

/// <summary>
/// <see cref="ITenantContextAccessor"/> implementation backed by <see cref="AsyncLocal{T}"/>
/// using the "holder pattern" to avoid the well-known AsyncLocal capture pitfall.
/// </summary>
/// <remarks>
/// <para>
/// The naive approach — <c>static AsyncLocal&lt;ITenantContext?&gt; _value</c> — has a subtle bug:
/// when a child Task (e.g. <c>Task.Run</c>) sets the <c>AsyncLocal</c>, the change is NOT visible
/// to the parent execution context because each child gets a copy-on-write snapshot. This means
/// the setter appears to work but the new value evaporates when the child Task exits.
/// </para>
/// <para>
/// The holder pattern fixes this by storing a mutable <em>object</em> (the holder) in the
/// <c>AsyncLocal</c> and mutating a property on that object. The holder reference is the same
/// in parent and child; mutations to the holder's property are visible across the shared reference.
/// This is the same approach used by <c>IHttpContextAccessor</c> in ASP.NET Core.
/// </para>
/// <para>
/// Registered as a singleton. Thread-safe: <see cref="ITenantContext"/> is immutable; the
/// holder property write is atomic on all .NET-supported platforms for reference types.
/// </para>
/// </remarks>
public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantContextHolder> _holder = new AsyncLocal<TenantContextHolder>();

    /// <inheritdoc />
    public ITenantContext? Current
    {
        get => _holder.Value?.TenantContext;
        set
        {
            // If a holder already exists, null its context so child execution contexts
            // spawned before this assignment do not unexpectedly see the new value.
            // The null-conditional operator cannot appear on the left side of an assignment,
            // so the explicit null check is intentional — suppress IDE0031.
#pragma warning disable IDE0031 // Null check can be simplified
            TenantContextHolder? existing = _holder.Value;
            if (existing != null)
            {
                existing.TenantContext = null;
            }
#pragma warning restore IDE0031

            _holder.Value = new TenantContextHolder { TenantContext = value };
        }
    }

    /// <inheritdoc />
    public bool IsBypassed => _holder.Value?.IsBypassed ?? false;

    /// <inheritdoc />
    public IDisposable BeginBypass()
    {
        // Ensure a holder exists so the bypass flag is visible to the current scope.
#pragma warning disable IDE0031 // Null check can be simplified
        TenantContextHolder? existing = _holder.Value;
        if (existing == null)
        {
            existing = new TenantContextHolder();
            _holder.Value = existing;
        }
#pragma warning restore IDE0031

        existing.IsBypassed = true;
        return new BypassHandle(existing);
    }

    /// <summary>
    /// Mutable wrapper stored in <see cref="AsyncLocal{T}"/>.
    /// The holder reference is shared across async continuations; mutating
    /// properties on the shared instance is the key to the pattern.
    /// </summary>
    private sealed class TenantContextHolder
    {
        public ITenantContext? TenantContext { get; set; }

        public bool IsBypassed { get; set; }
    }

    /// <summary>
    /// Restores <see cref="TenantContextHolder.IsBypassed"/> to <see langword="false"/>
    /// when disposed. Uses the holder reference captured at <see cref="BeginBypass"/> time.
    /// </summary>
    private sealed class BypassHandle : IDisposable
    {
        private readonly TenantContextHolder _holder;

        public BypassHandle(TenantContextHolder holder)
        {
            _holder = holder;
        }

        public void Dispose()
        {
            _holder.IsBypassed = false;
        }
    }
}
