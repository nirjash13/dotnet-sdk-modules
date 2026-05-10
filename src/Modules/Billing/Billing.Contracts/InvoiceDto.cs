using System;

namespace Billing.Contracts;

/// <summary>
/// Data transfer object for a billing invoice line item.
/// </summary>
public sealed record InvoiceDto(
    Guid Id,
    Guid TenantId,
    string ProviderInvoiceId,
    long AmountDueCents,
    string Currency,
    string Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset? DueAt,
    string? HostedInvoiceUrl);
