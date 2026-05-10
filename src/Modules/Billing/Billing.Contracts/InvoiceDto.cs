using System;

namespace Billing.Contracts;

/// <summary>
/// Data transfer object for a billing invoice.
/// </summary>
public sealed class InvoiceDto
{
    /// <summary>Initializes all required fields.</summary>
    public InvoiceDto(
        Guid id,
        Guid tenantId,
        string providerInvoiceId,
        long amountDueCents,
        string currency,
        string status,
        DateTimeOffset issuedAt,
        DateTimeOffset? dueAt,
        string? hostedInvoiceUrl)
    {
        Id = id;
        TenantId = tenantId;
        ProviderInvoiceId = providerInvoiceId ?? throw new ArgumentNullException(nameof(providerInvoiceId));
        AmountDueCents = amountDueCents;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        Status = status ?? throw new ArgumentNullException(nameof(status));
        IssuedAt = issuedAt;
        DueAt = dueAt;
        HostedInvoiceUrl = hostedInvoiceUrl;
    }

    /// <summary>Gets the invoice identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the tenant identifier.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the provider-side invoice identifier.</summary>
    public string ProviderInvoiceId { get; }

    /// <summary>Gets the amount due in the smallest currency unit (cents/pence).</summary>
    public long AmountDueCents { get; }

    /// <summary>Gets the 3-character ISO 4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Gets the invoice status (e.g., "draft", "open", "paid", "void").</summary>
    public string Status { get; }

    /// <summary>Gets the UTC timestamp when the invoice was issued.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>Gets the invoice due date, if applicable.</summary>
    public DateTimeOffset? DueAt { get; }

    /// <summary>Gets the hosted invoice URL for the customer to view/pay, if available.</summary>
    public string? HostedInvoiceUrl { get; }
}
