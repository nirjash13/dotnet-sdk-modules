using System;
using System.Threading.Tasks;
using Identity.Application.Services;
using Identity.Domain.Entities;
using MassTransit;
using Registration.Contracts;

namespace Identity.Application.Consumers;

/// <summary>
/// Handles the <see cref="CreateUser"/> command sent by the registration saga.
/// Creates a stub user in the Identity store and responds with <see cref="UserCreated"/>.
/// </summary>
/// <remarks>
/// This is a minimal stub implementation — full password-hashing, email verification, and
/// OpenIddict application registration are deferred to Phase 6+ as noted in the plan.
/// Idempotency: if a user with the same email already exists, the existing user id is returned.
/// </remarks>
public sealed class CreateUserCommandConsumer(IUserRepository userRepository) : IConsumer<CreateUser>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<CreateUser> context)
    {
        CreateUser command = context.Message;

        // Idempotent: if user already exists (e.g. retry), return existing user id.
        User? existing = await userRepository
            .FindByEmailAsync(command.Email, context.CancellationToken)
            .ConfigureAwait(false);

        Guid userId;
        if (existing is not null)
        {
            userId = existing.Id;
        }
        else
        {
            User user = User.Create(Guid.NewGuid(), command.Email, command.DisplayName);
            userRepository.Add(user);
            await userRepository.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
            userId = user.Id;
        }

        await context.Publish(new UserCreated
        {
            CorrelationId = command.CorrelationId,
            UserId = userId,
        }).ConfigureAwait(false);
    }
}
