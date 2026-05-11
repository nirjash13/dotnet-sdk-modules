using SaasBuilder.Host.Configuration;
using SaasBuilder.SharedKernel.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddSaasBuilderHost(opts =>
{
    opts.UseTransport(SaasTransport.InProc);

    // B2C pattern: PoolShared — single DB, no RLS (per-user tenant auto-created on signup)
    opts.UseTenancy(TenantIsolation.PoolShared);
    opts.Observability.Enable();

    // Minimal module set for B2C apps
    opts.Modules.ScanAssemblyContaining<Identity.Api.IdentityModule>();
    opts.Modules.ScanAssemblyContaining<Billing.Api.BillingModule>();
    opts.Modules.ScanAssemblyContaining<Notifications.Api.NotificationsModule>();
});

WebApplication app = builder.Build();
app.UseSaasBuilderPipeline();

await app.RunAsync().ConfigureAwait(false);

namespace B2CSample.Host
{
    /// <summary>
    /// Partial extension of the compiler-generated Program class so integration tests
    /// can reference it via <c>WebApplicationFactory&lt;Program&gt;</c>.
    /// </summary>
    public partial class Program
    {
    }
}
