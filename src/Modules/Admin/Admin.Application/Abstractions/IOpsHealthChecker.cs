using System.Threading;
using System.Threading.Tasks;
using Admin.Contracts;

namespace Admin.Application.Abstractions;

/// <summary>
/// Checks the operational health of core platform components (DB, queue, providers, SLO).
/// </summary>
public interface IOpsHealthChecker
{
    /// <summary>Runs all health probes and returns the aggregated status.</summary>
    Task<OpsHealthDto> CheckAsync(CancellationToken ct = default);
}
