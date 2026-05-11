using SaasBuilder.Host.Configuration;
using SaasBuilder.SharedKernel.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddSaasBuilderHost(opts =>
{
    opts.UseTransport(SaasTransport.InProc);
    opts.UseTenancy(TenantIsolation.PoolWithRls);
    opts.Observability.Enable();
    opts.RateLimiting.UsePerTenantSlidingWindow();

    // B2B pattern: scan all module startups from referenced assemblies
    opts.Modules.ScanAssemblyContaining<Identity.Api.IdentityModule>();
    opts.Modules.ScanAssemblyContaining<Billing.Api.BillingModule>();
    opts.Modules.ScanAssemblyContaining<Notifications.Api.NotificationsModule>();
    opts.Modules.ScanAssemblyContaining<Files.Api.FilesModule>();
    opts.Modules.ScanAssemblyContaining<Audit.Api.AuditModule>();
    opts.Modules.ScanAssemblyContaining<Webhooks.Api.WebhooksModule>();
    opts.Modules.ScanAssemblyContaining<Admin.Api.AdminModule>();
});

WebApplication app = builder.Build();
app.UseSaasBuilderPipeline();

await app.RunAsync().ConfigureAwait(false);

namespace B2BSample.Host
{
    /// <summary>
    /// Partial extension of the compiler-generated Program class so integration tests
    /// can reference it via <c>WebApplicationFactory&lt;Program&gt;</c>.
    /// </summary>
    public partial class Program
    {
    }
}
