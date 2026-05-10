using SaasBuilder.Host.Configuration;
using SaasBuilder.SharedKernel.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddSaasBuilderHost(opts =>
{
    opts.UseTransport(SaasTransport.InProc);
    opts.UseTenancy(TenantIsolation.PoolWithRls);
    opts.Observability.Enable();
    opts.RateLimiting.UsePerTenantSlidingWindow();
});

WebApplication app = builder.Build();
app.UseSaasBuilderPipeline();

await app.RunAsync().ConfigureAwait(false);
