using System;

namespace Gdpr.Application.Commands;

/// <summary>Creates an erasure (right-to-be-forgotten) request for the calling user.</summary>
public sealed record RequestErasureCommand(Guid TenantId, Guid UserId);
