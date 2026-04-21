using System;
using System.Collections.Generic;

namespace Identity.Contracts;

/// <summary>
/// DTO representing the currently authenticated principal's identity claims.
/// Returned from the <c>GET /api/v1/identity/me</c> endpoint.
/// </summary>
public sealed class ClaimsPrincipalDto
{
    /// <summary>Gets or sets the subject (user id) from the <c>sub</c> claim.</summary>
    public string? Sub { get; set; }

    /// <summary>Gets or sets the tenant identifier from the <c>tenant_id</c> claim.</summary>
    public string? TenantId { get; set; }

    /// <summary>Gets or sets the roles from the <c>role</c> claims.</summary>
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the display name from the <c>name</c> claim.</summary>
    public string? Name { get; set; }
}
