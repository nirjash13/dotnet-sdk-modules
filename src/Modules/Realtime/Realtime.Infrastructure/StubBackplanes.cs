namespace Realtime.Infrastructure;

// ---------------------------------------------------------------------------
// Backplane stubs — Phase 5.7
// ---------------------------------------------------------------------------

/// <summary>
/// TODO(Phase 5.7): Redis backplane for SignalR.
/// Install Microsoft.AspNetCore.SignalR.StackExchangeRedis NuGet package.
/// Enable via: builder.Services.AddSignalR().AddStackExchangeRedis(connectionString);
/// Redis backplane is in the ASP.NET Core shared framework — no additional dependencies beyond
/// StackExchange.Redis (included by the backplane NuGet package).
/// </summary>
internal static class RedisBackplane
{
    internal const string TodoNote = "TODO(Phase 5.7): install Microsoft.AspNetCore.SignalR.StackExchangeRedis";
}

/// <summary>
/// TODO(Phase 5.7): SQL Server backplane for SignalR.
/// Install Microsoft.AspNetCore.SignalR.SqlServer NuGet package.
/// </summary>
internal static class SqlBackplane
{
    internal const string TodoNote = "TODO(Phase 5.7): install Microsoft.AspNetCore.SignalR.SqlServer";
}
