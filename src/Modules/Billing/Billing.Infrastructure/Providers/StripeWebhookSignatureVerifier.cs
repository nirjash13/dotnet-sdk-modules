using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Billing.Infrastructure.Providers;

/// <summary>
/// Verifies Stripe webhook signatures using the Stripe-Signature HMAC-SHA256 scheme.
/// Enforces a 5-minute timestamp replay window.
///
/// Stripe-Signature header format: t=&lt;unix_timestamp&gt;,v1=&lt;hex_signature&gt;
/// Signed payload: "{timestamp}.{rawBody}"
/// </summary>
public sealed class StripeWebhookSignatureVerifier(IConfiguration configuration) : IWebhookSignatureVerifier
{
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public string ProviderName => "stripe";

    /// <inheritdoc />
    public Task<WebhookVerificationResult> VerifyAsync(
        byte[] rawBody,
        string signatureHeader,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return Task.FromResult(WebhookVerificationResult.Failure("Missing Stripe-Signature header."));
        }

        long timestampUnix = 0;
        var v1Signatures = new System.Collections.Generic.List<string>();

        foreach (string part in signatureHeader.Split(','))
        {
            string[] kv = part.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }

            if (kv[0] == "t" && long.TryParse(kv[1], out long ts))
            {
                timestampUnix = ts;
            }
            else if (kv[0] == "v1")
            {
                v1Signatures.Add(kv[1]);
            }
        }

        if (timestampUnix == 0 || v1Signatures.Count == 0)
        {
            return Task.FromResult(WebhookVerificationResult.Failure("Malformed Stripe-Signature header."));
        }

        // Enforce 5-minute replay window.
        DateTimeOffset eventTime = DateTimeOffset.FromUnixTimeSeconds(timestampUnix);
        if (DateTimeOffset.UtcNow - eventTime > ReplayWindow)
        {
            return Task.FromResult(WebhookVerificationResult.Failure(
                $"Webhook timestamp is outside the {ReplayWindow.TotalMinutes}-minute replay window."));
        }

        string? webhookSecret = configuration["Billing:Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return Task.FromResult(WebhookVerificationResult.Failure(
                "Billing:Stripe:WebhookSecret is not configured."));
        }

        // Compute expected signature: HMAC-SHA256(webhook_secret, "{timestamp}.{body}")
        string signedPayload = $"{timestampUnix}.{Encoding.UTF8.GetString(rawBody)}";
        string expectedSignature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(webhookSecret),
            Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();

        byte[] expectedSignatureBytes2 = Encoding.UTF8.GetBytes(expectedSignature);
        bool anyMatch = false;
        foreach (string v1 in v1Signatures)
        {
            if (CryptographicOperations.FixedTimeEquals(expectedSignatureBytes2, Encoding.UTF8.GetBytes(v1)))
            {
                anyMatch = true;
                break;
            }
        }

        if (!anyMatch)
        {
            return Task.FromResult(WebhookVerificationResult.Failure("HMAC signature mismatch."));
        }

        // Parse the body JSON to extract event type and event ID for idempotency.
        string? eventType = null;
        string? eventId = null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.TryGetProperty("type", out JsonElement typeEl))
            {
                eventType = typeEl.GetString();
            }

            if (doc.RootElement.TryGetProperty("id", out JsonElement idEl))
            {
                eventId = idEl.GetString();
            }
        }
        catch (JsonException)
        {
            // Body is not valid JSON — still valid from signature standpoint.
            // Fall through: SHA-256 fallback below produces a collision-free idempotency key.
        }

        // Fallback: SHA-256 of the raw payload bytes (hex). This is collision-free regardless
        // of whether JSON parsing succeeded, replacing the prior timestamp-only fallback which
        // would collide for two distinct payloads delivered within the same second.
        // The "stripe_sha256_" prefix ensures this key never collides with idempotency keys
        // from other providers (e.g. Paddle, Lemon Squeezy) that share the same webhook_events table.
        string idempotencyKey = eventId ?? $"stripe_sha256_{Convert.ToHexString(SHA256.HashData(rawBody)).ToLowerInvariant()}";
        string resolvedEventType = eventType ?? "webhook.unknown";

        return Task.FromResult(WebhookVerificationResult.Success(resolvedEventType, idempotencyKey));
    }
}
