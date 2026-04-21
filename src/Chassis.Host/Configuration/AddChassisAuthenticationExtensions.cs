using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Chassis.Host.Configuration;

/// <summary>
/// Extension method that wires JWT Bearer validation pointing at the Identity module's
/// OpenIddict OIDC discovery endpoint.
/// </summary>
public static class AddChassisAuthenticationExtensions
{
    /// <summary>
    /// Configures JWT Bearer authentication backed by the Identity module (OpenIddict).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration keys (all under the <c>Identity</c> section):
    /// <list type="table">
    ///   <listheader><term>Key</term><description>Purpose</description></listheader>
    ///   <item><term>Identity:Authority</term><description>Base URL of the OpenIddict host (e.g. https://localhost:5001).</description></item>
    ///   <item><term>Identity:Audience</term><description>Expected <c>aud</c> claim value. Defaults to <c>chassis-api</c>.</description></item>
    ///   <item><term>Identity:AllowInsecureMetadata</term><description>When <c>true</c>, disables HTTPS requirement for metadata fetch. Dev only.</description></item>
    /// </para>
    /// <para>
    /// <b>In-proc metadata deadlock risk:</b>
    /// The Identity module and Chassis.Host run in the same process. The JwtBearer middleware
    /// normally fetches OIDC discovery metadata from <c>Authority/.well-known/openid-configuration</c>
    /// on the first authenticated request. When both host and identity share the same process and
    /// port, this HTTP call would loop back to the same Kestrel instance — which is safe because
    /// ASP.NET Core Kestrel handles concurrent requests on separate I/O threads (no thread-pool
    /// starvation risk). However, during the very first request the server is already inside the
    /// middleware pipeline, so a synchronous back-channel call could deadlock.
    ///
    /// Resolution: <c>BackchannelHttpHandler</c> is set to a <see cref="HttpClientHandler"/> with
    /// a short timeout only in dev (<c>AllowInsecureMetadata = true</c>). The JwtBearer middleware
    /// always uses async HTTP internally (no sync-over-async), so the loopback is safe as long as
    /// Kestrel has free request threads — which it does because the back-channel fetch happens on a
    /// thread-pool thread, not inside the request pipeline thread itself. No additional workaround
    /// (such as a manually constructed <see cref="Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration"/>)
    /// is needed: the built-in metadata manager with async fetch is the correct approach per
    /// Microsoft's JwtBearer source and the ASP.NET Core docs.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to register authentication into.</param>
    /// <param name="configuration">Application configuration providing Identity section values.</param>
    /// <param name="environment">The host environment; required to gate the insecure metadata handler.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddChassisAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        string? authority = configuration["Identity:Authority"];
        string audience = configuration["Identity:Audience"] ?? "chassis-api";
        bool allowInsecureMetadata = configuration.GetValue<bool>("Identity:AllowInsecureMetadata");

        // The dangerous cert validator is permitted only when BOTH conditions hold:
        // (a) the environment is Development, AND (b) AllowInsecureMetadata is true.
        // Checking only the config flag is insufficient — a misconfigured Staging environment
        // with AllowInsecureMetadata=true would otherwise skip certificate validation in prod.
        bool enableDangerousCertHandler = environment.IsDevelopment() && allowInsecureMetadata;

        // RequireHttpsMetadata defaults true; explicitly false only when in dev with the flag set.
        bool requireHttpsMetadata = !enableDangerousCertHandler;

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = requireHttpsMetadata;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),

                    // Map JWT standard claims to ASP.NET Core claim types so that
                    // User.Identity.Name returns the 'sub' value, and role-based
                    // authorization reads 'roles' rather than the long XML schema URI.
                    NameClaimType = "sub",
                    RoleClaimType = "roles",
                };

                // In dev (AllowInsecureMetadata = true), the Identity module and Host share
                // the same process. We provide a loopback-capable HttpClientHandler so the
                // metadata fetch does not reject self-signed or http-only endpoints.
                // In prod this block is skipped; the default backchannel handler is used.
                if (enableDangerousCertHandler)
                {
#pragma warning disable CA5386 // Do not hardcode SecurityProtocol; this is dev-only.
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        // Allow self-signed / HTTP metadata endpoint in Development only.
                        // enableDangerousCertHandler is false in Staging and Production.
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                    };
#pragma warning restore CA5386
                }

                // Produce RFC 7807 ProblemDetails on 401 challenges instead of the default
                // empty WWW-Authenticate header response. The global ProblemDetailsExceptionHandler
                // does not fire for auth challenges (they are not exceptions), so we handle this
                // specifically in the JwtBearer event.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        // Suppress the default challenge response (which only sets headers).
                        context.HandleResponse();

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";

                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = string.IsNullOrEmpty(context.ErrorDescription)
                                ? "A valid bearer token is required."
                                : context.ErrorDescription,
                            Extensions = { ["code"] = "unauthorized" },
                        };

                        await context.Response
                            .WriteAsJsonAsync(problem, context.HttpContext.RequestAborted)
                            .ConfigureAwait(false);
                    },
                };
            });

        services.AddAuthorization();

        return services;
    }
}
