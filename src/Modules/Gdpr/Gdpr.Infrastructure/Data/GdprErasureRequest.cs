using System;
using Gdpr.Contracts;
using SaasBuilder.SharedKernel.Tenancy;

namespace Gdpr.Infrastructure.Data;

/// <summary>Erasure (right-to-be-forgotten) request stored in <c>gdpr_erasure_requests</c>.</summary>
internal sealed class GdprErasureRequest : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public ErasureStatus Status { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset GraceEndsAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
