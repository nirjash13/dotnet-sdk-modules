using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace __ModuleName__.Api;

/// <summary>Sample endpoint — replace with real feature endpoints.</summary>
internal static class SampleEndpoint
{
    internal static IEndpointRouteBuilder Map__ModuleName__Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/__module_route__")
            .RequireAuthorization()
            .WithTags("__ModuleName__");

        group.MapGet("/health", () => Results.Ok(new { module = "__ModuleName__", status = "ok" }))
            .AllowAnonymous()
            .WithName("Get__ModuleName__Health");

        return app;
    }
}
