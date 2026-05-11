using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace SaasBuilder.HostedUi;

/// <summary>
/// Extension methods to register SaasBuilder Hosted UI pages.
/// </summary>
public static class SaasBuilderHostedUiExtensions
{
    /// <summary>
    /// Adds the SaasBuilder Hosted UI (Razor Pages) to the application.
    /// Pages are served from the <c>SaasBuilder.HostedUi</c> assembly under:
    /// <list type="bullet">
    ///   <item><c>/Identity/Login</c></item>
    ///   <item><c>/Identity/MfaSetup</c></item>
    ///   <item><c>/Identity/AcceptInvitation</c></item>
    ///   <item><c>/Billing/PortalRedirect</c></item>
    /// </list>
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/>.</param>
    /// <returns>The same <see cref="WebApplicationBuilder"/> for chaining.</returns>
    public static WebApplicationBuilder AddSaasBuilderHostedUi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddRazorPages()
            .ConfigureApplicationPartManager(apm =>
            {
                // Ensure the HostedUi assembly is registered as an application part
                // so its Razor Pages and areas are discoverable by the runtime.
                var hostedUiAssembly = typeof(SaasBuilderHostedUiExtensions).Assembly;
                if (!apm.ApplicationParts.Any(p => p.Name == hostedUiAssembly.GetName().Name))
                {
                    apm.ApplicationParts.Add(new AssemblyPart(hostedUiAssembly));
                }
            });

        builder.Services.AddControllersWithViews();

        return builder;
    }
}
