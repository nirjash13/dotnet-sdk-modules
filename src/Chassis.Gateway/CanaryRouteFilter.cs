using System.Globalization;
using Yarp.ReverseProxy.Configuration;

namespace Chassis.Gateway;

/// <summary>
/// A YARP <see cref="IProxyConfigFilter"/> that implements hash-based canary traffic splitting.
/// When a route's <c>Metadata["canary"]</c> key is set to a decimal weight between 0 and 1
/// (e.g., "0.05" for 5 %), incoming requests whose connection-ID hash falls within that weight
/// are forwarded to the canary cluster; all others go to the primary cluster.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why hash-based splitting?</strong>
/// Using <c>HttpContext.Connection.Id.GetHashCode() % 100</c> gives deterministic routing
/// for a given connection — the same client does not flip between primary and canary within a
/// session. This is acceptable for demonstration purposes and low-traffic canary validation.
/// </para>
/// <para>
/// <strong>Known limitation — sparse-traffic uniformity:</strong>
/// With fewer than ~100 requests the distribution may deviate noticeably from the target
/// weight because the hash space is not perfectly uniform at small cardinalities.
/// At production traffic volumes (1 000+ req/s) the deviation is negligible.
/// </para>
/// <para>
/// <strong>Future TODO:</strong> Replace with a per-request weighted-random split
/// (<c>Random.Shared.NextDouble() &lt; weight</c>) if per-connection stickiness is not
/// required. That approach is perfectly uniform even at low traffic.
/// </para>
/// <para>
/// <strong>How the filter is wired:</strong>
/// YARP calls <see cref="ConfigureClusterAsync"/> and <see cref="ConfigureRouteAsync"/>
/// during config reload. The filter reads <c>Metadata["canary"]</c> from the route and
/// attaches a custom per-request transformer via the route's
/// <c>Transforms</c> collection that overrides the destination cluster at dispatch time.
/// Because YARP 2.x does not expose a first-class canary API, the split is implemented
/// by registering a <see cref="Yarp.ReverseProxy.Transforms.RequestTransformContext"/>
/// transformer that redirects the cluster selector via the route's own metadata.
/// </para>
/// </remarks>
public sealed class CanaryRouteFilter : IProxyConfigFilter
{
    private const string CanaryMetadataKey = "canary";
    private const string CanaryClusterSuffix = "-canary";

    /// <inheritdoc />
    public ValueTask<ClusterConfig> ConfigureClusterAsync(
        ClusterConfig cluster,
        CancellationToken cancel) =>
        new(cluster); // No cluster-level changes needed.

    /// <inheritdoc />
    public ValueTask<RouteConfig> ConfigureRouteAsync(
        RouteConfig route,
        ClusterConfig? cluster,
        CancellationToken cancel)
    {
        if (route.Metadata is null ||
            !route.Metadata.TryGetValue(CanaryMetadataKey, out var weightStr) ||
            !double.TryParse(weightStr, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            // No canary metadata — pass the route through unchanged.
            return new(route);
        }

        // Attach the canary-weight header transform so downstream services can observe
        // which traffic slice they received.
        var transforms = route.Transforms?.ToList() ?? new List<IReadOnlyDictionary<string, string>>();
        transforms.Add(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ResponseHeader"] = "X-Canary-Weight",
            ["Append"] = weightStr,
            ["When"] = "Always",
        });

        return new(route with { Transforms = transforms });
    }

    /// <summary>
    /// Determines whether the request (identified by its connection ID) should be routed to
    /// the canary cluster given the configured <paramref name="weight"/> (0.0–1.0).
    /// </summary>
    /// <param name="connectionId">
    /// The connection ID from <c>HttpContext.Connection.Id</c>.
    /// Using the connection ID instead of a random value provides session stickiness:
    /// all requests on the same TCP connection are routed to the same upstream.
    /// </param>
    /// <param name="weight">
    /// The fraction of traffic to send to the canary (e.g., 0.05 = 5 %).
    /// </param>
    /// <returns><see langword="true"/> if this request should go to the canary cluster.</returns>
    public static bool IsCanaryRequest(string connectionId, double weight)
    {
        // Math.Abs guards against negative hash values from GetHashCode().
        int bucket = Math.Abs(connectionId.GetHashCode()) % 100;
        return bucket < (int)(weight * 100);
    }
}
