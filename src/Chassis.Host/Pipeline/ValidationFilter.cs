using System;
using System.Threading.Tasks;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Chassis.Host.Pipeline;

/// <summary>
/// MassTransit consume filter that validates the incoming message using FluentValidation
/// before the handler executes.
/// </summary>
/// <remarks>
/// Resolves <c>IValidator&lt;T&gt;</c> from the scoped DI container via the consume context's
/// service provider. If no validator is registered for <typeparamref name="T"/>, the message
/// passes through without validation — validators are opt-in per message type.
/// Throws <see cref="ValidationException"/> on failure; the ProblemDetails middleware
/// maps this to HTTP 400 with <c>code=validation_failed</c>.
/// </remarks>
internal sealed class ValidationFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("validation");
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        IValidator<T>? validator = context.GetPayload<IServiceProvider>()
            .GetService<IValidator<T>>();

        if (validator is not null)
        {
            FluentValidation.Results.ValidationResult result =
                await validator.ValidateAsync(context.Message, context.CancellationToken)
                    .ConfigureAwait(false);

            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next.Send(context).ConfigureAwait(false);
    }
}
