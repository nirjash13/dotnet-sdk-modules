using System;

namespace Registration.Contracts;

/// <summary>Command sent by the saga to the Identity module to create the primary user.</summary>
public class CreateUser
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant being provisioned.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the email address for the new user.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name for the new user.</summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>Response sent by the Identity consumer when user creation succeeds.</summary>
public class UserCreated
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the newly created user identifier.</summary>
    public Guid UserId { get; set; }
}

/// <summary>Compensation command sent by the saga to the Identity module to roll back user creation.</summary>
public class DeleteUser
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the user identifier to delete.</summary>
    public Guid UserId { get; set; }
}

/// <summary>Response sent by the Identity consumer when user deletion succeeds.</summary>
public class UserDeleted
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the deleted user identifier.</summary>
    public Guid UserId { get; set; }
}
