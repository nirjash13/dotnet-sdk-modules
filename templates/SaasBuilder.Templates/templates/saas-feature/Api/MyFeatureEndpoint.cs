using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyModule.Application.MyFeature;

namespace MyModule.Api;

/// <summary>HTTP endpoint for the MyFeature feature.</summary>
internal static class MyFeatureEndpoint
{
    internal static IEndpointRouteBuilder MapMyFeature(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/v1/mymodule/myfeature",
            async (
                IRequestClient<MyFeatureRequest> client,
                MyFeatureRequest request,
                CancellationToken ct) =>
            {
                var response = await client
                    .GetResponse<MyFeatureResponse>(request, ct)
                    .ConfigureAwait(false);

                return Results.Ok(response.Message);
            })
            .RequireAuthorization()
            .WithName("MyFeature")
            .WithTags("MyModule")
            .ProducesValidationProblem()
            .Produces<MyFeatureResponse>();

        return app;
    }
}
