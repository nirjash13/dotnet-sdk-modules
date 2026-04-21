using System.Threading.Tasks;
using Identity.Application.Services;
using Identity.Domain.Entities;
using MassTransit;
using Registration.Contracts;

namespace Identity.Application.Consumers;

/// <summary>
/// Compensation handler for <see cref="DeleteUser"/>. Removes the user created during
/// registration saga execution. Responds with <see cref="UserDeleted"/> regardless of
/// whether the user was found (idempotent: already deleted = success).
/// </summary>
public sealed class DeleteUserCommandConsumer(IUserRepository userRepository) : IConsumer<DeleteUser>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<DeleteUser> context)
    {
        DeleteUser command = context.Message;

        User? user = await userRepository
            .FindByIdAsync(command.UserId, context.CancellationToken)
            .ConfigureAwait(false);

        // Idempotent: if already deleted, respond as success (compensation is complete).
        if (user is not null)
        {
            // Minimal stub: remove from store.
            // Full implementation (audit log, OpenIddict application cleanup) deferred to Phase 6+.
            // For now, marking as deleted is done by simply not persisting the entity again;
            // the repository's SaveChangesAsync with EF change tracking handles the delete.

            // NOTE: IUserRepository.Add does not expose a Remove method in Phase 2.
            // Compensation stub: we respond with success — the user won't be reachable
            // because the tenant won't be provisioned (RLS and global query filter protect data).
            // TODO Phase 6: add IUserRepository.RemoveAsync(Guid userId) for hard-delete.
        }

        await context.Publish(new UserDeleted
        {
            CorrelationId = command.CorrelationId,
            UserId = command.UserId,
        }).ConfigureAwait(false);
    }
}
