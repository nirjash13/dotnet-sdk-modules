using System.Threading;
using System.Threading.Tasks;

namespace Billing.Application.Abstractions;

/// <summary>
/// Verifies the HMAC signature on incoming webhook requests.
/// Each billing provider has a distinct signing scheme; implementations are
/// registered per-provider and resolved by provider name at runtime.
/// </summary>
public interface IWebhookSignatureVerifier
{
    /// <summary>Gets the provider name this verifier handles (e.g., "stripe").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Verifies the webhook signature and returns a decoded event payload on success.
    /// </summary>
    /// <param name="rawBody">The raw request body bytes.</param>
    /// <param name="signatureHeader">The provider's signature header value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="WebhookVerificationResult"/> containing the provider event type and idempotency key,
    /// or a failure result when the signature is invalid or the timestamp is outside the replay window.
    /// </returns>
    Task<WebhookVerificationResult> VerifyAsync(
        byte[] rawBody,
        string signatureHeader,
        CancellationToken ct = default);
}

/// <summary>
/// Result of webhook signature verification.
/// </summary>
public sealed record WebhookVerificationResult
{
    /// <summary>Gets a successful verification result.</summary>
    public static WebhookVerificationResult Success(string eventType, string idempotencyKey) =>
        new WebhookVerificationResult(true, eventType, idempotencyKey, null);

    /// <summary>Gets a failed verification result.</summary>
    public static WebhookVerificationResult Failure(string reason) =>
        new WebhookVerificationResult(false, null, null, reason);

    private WebhookVerificationResult(
        bool isValid,
        string? eventType,
        string? idempotencyKey,
        string? failureReason)
    {
        IsValid = isValid;
        EventType = eventType;
        IdempotencyKey = idempotencyKey;
        FailureReason = failureReason;
    }

    /// <summary>Gets a value indicating whether the signature and timestamp are valid.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the provider event type (e.g., "customer.subscription.updated") on success.</summary>
    public string? EventType { get; }

    /// <summary>Gets the idempotency key for dedup (typically the provider event id) on success.</summary>
    public string? IdempotencyKey { get; }

    /// <summary>Gets the human-readable failure reason when <see cref="IsValid"/> is false.</summary>
    public string? FailureReason { get; }
}
