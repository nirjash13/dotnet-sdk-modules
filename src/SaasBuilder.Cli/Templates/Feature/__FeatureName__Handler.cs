using MassTransit;
using Microsoft.Extensions.Logging;

namespace __ModuleName__.Application.__FeatureName__;

/// <summary>Handler for <see cref="__FeatureName__Request"/>.</summary>
internal sealed class __FeatureName__Handler(ILogger<__FeatureName__Handler> logger)
    : IConsumer<__FeatureName__Request>
{
    public async Task Consume(ConsumeContext<__FeatureName__Request> context)
    {
        logger.LogInformation("Handling {Feature}", nameof(__FeatureName__));

        // TODO: implement business logic

        await context.RespondAsync(new __FeatureName__Response(
            // TODO: populate response
        )).ConfigureAwait(false);
    }
}
