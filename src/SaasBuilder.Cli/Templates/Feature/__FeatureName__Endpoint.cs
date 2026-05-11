using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace __ModuleName__.Api;

/// <summary>HTTP endpoint for the __FeatureName__ feature.</summary>
internal static class __FeatureName__Endpoint
{
    internal static IEndpointRouteBuilder Map__FeatureName__(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/__module_route__/__feature_route__",
            async (IRequestClient<__ModuleName__.Application.__FeatureName__.__FeatureName__Request> client,
                   __ModuleName__.Application.__FeatureName__.__FeatureName__Request request,
                   CancellationToken ct) =>
            {
                var response = await client.GetResponse<__ModuleName__.Application.__FeatureName__.__FeatureName__Response>(request, ct)
                    .ConfigureAwait(false);
                return Results.Ok(response.Message);
            })
            .RequireAuthorization()
            .WithName("__FeatureName__")
            .WithTags("__ModuleName__")
            .ProducesValidationProblem()
            .Produces<__ModuleName__.Application.__FeatureName__.__FeatureName__Response>();

        return app;
    }
}
