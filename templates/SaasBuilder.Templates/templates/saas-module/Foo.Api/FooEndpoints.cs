using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Foo.Api;

/// <summary>Foo module endpoint registrations.</summary>
internal static class FooEndpoints
{
    internal static IEndpointRouteBuilder MapFooEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/foo")
            .RequireAuthorization()
            .WithTags("Foo");

        group.MapGet("/health", () => Results.Ok(new { module = "Foo", status = "ok" }))
            .AllowAnonymous()
            .WithName("GetFooHealth");

        return app;
    }
}
