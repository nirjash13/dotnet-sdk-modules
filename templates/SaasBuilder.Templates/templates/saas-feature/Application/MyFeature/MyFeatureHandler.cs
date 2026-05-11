using MassTransit;
using Microsoft.Extensions.Logging;

namespace MyModule.Application.MyFeature;

/// <summary>Handler for <see cref="MyFeatureRequest"/>.</summary>
internal sealed class MyFeatureHandler(ILogger<MyFeatureHandler> logger)
    : IConsumer<MyFeatureRequest>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<MyFeatureRequest> context)
    {
        logger.LogInformation("Handling {Feature}", nameof(MyFeature));

        // TODO: implement business logic

        await context.RespondAsync(new MyFeatureResponse(
            // TODO: populate response
        )).ConfigureAwait(false);
    }
}
