using Chassis.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Infrastructure.Extensions;

namespace Reporting.Api;

/// <summary>
/// <see cref="IModuleStartup"/> implementation for the Reporting module.
/// Discovered by <c>ReflectionModuleLoader</c> at startup via assembly scan.
/// </summary>
/// <remarks>
/// Phase 4 registers only the infrastructure (DbContext, IReportingDbContext).
/// The consumer <c>LedgerTransactionPostedConsumer</c> is registered in the bus wiring
/// (<c>MassTransitConfig.AddChassisBus</c> in Chassis.Host) rather than here, following
/// the same pattern as Ledger command/query handlers being registered in LedgerModule.
/// Phase 5 will add HTTP query endpoints for projection data.
/// </remarks>
public sealed class ReportingModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddReportingInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        // No HTTP endpoints in Phase 4.
        // Phase 5 will add GET /api/v1/reporting/projections.
    }
}
