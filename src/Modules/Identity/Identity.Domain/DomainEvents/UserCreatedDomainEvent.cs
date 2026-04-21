using System;
using Chassis.SharedKernel.Abstractions;

namespace Identity.Domain.DomainEvents;

/// <summary>
/// Domain event raised when a new <see cref="Identity.Domain.Entities.User"/> is created.
/// </summary>
public sealed class UserCreatedDomainEvent : IDomainEvent
{
    /// <summary>Initializes a new instance of <see cref="UserCreatedDomainEvent"/>.</summary>
    public UserCreatedDomainEvent(Guid userId, string email)
    {
        UserId = userId;
        Email = email;
    }

    /// <summary>Gets the user identifier.</summary>
    public Guid UserId { get; }

    /// <summary>Gets the user's email address.</summary>
    public string Email { get; }
}
