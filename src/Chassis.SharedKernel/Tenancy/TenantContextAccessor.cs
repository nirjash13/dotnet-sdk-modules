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
/// <c>AsyncLocal</c> and mutating a field on that object. The holder reference is the same
/// in parent and child; mutations to the holder's field are visible across the shared reference.
/// This is the same approach used by <c>IHttpContextAccessor</c> in ASP.NET Core.
/// </para>
/// <para>
/// Registered as a singleton. Thread-safe: <see cref="ITenantContext"/> is immutable; the
/// holder field is a plain reference write (atomic on all .NET-supported platforms for
/// reference types).
/// </para>
/// </remarks>
public sealed class TenantContextAccessor : ITenantContextAccessor
{
    // The holder class wraps the mutable value. Because we store the holder itself in the
    // AsyncLocal (not the ITenantContext directly), mutations to holder.Value are visible to
    // all code sharing the same holder reference — including async continuations started
    // before the assignment.
    private sealed class TenantContextHolder
    {
        public ITenantContext? Value;
    }

    private static readonly AsyncLocal<TenantContextHolder> _holder = new();

    /// <inheritdoc />
    public ITenantContext? Current
    {
        get => _holder.Value?.Value;
        set
        {
            // Ensure a holder exists in this execution context before writing.
            // If no holder exists yet, create one so the write propagates correctly.
            var holder = _holder.Value;
            if (holder is not null)
            {
                // Wipe the existing holder so child contexts spawned before this set
                // don't unexpectedly see the new value (avoid upward flow).
                holder.Value = null;
            }

            _holder.Value = new TenantContextHolder { Value = value };
        }
    }
}
