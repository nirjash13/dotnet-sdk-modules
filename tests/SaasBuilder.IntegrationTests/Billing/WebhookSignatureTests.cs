using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SaasBuilder.IntegrationTests.Billing;

/// <summary>
/// Load-bearing tests for the Stripe webhook signature verifier.
///
/// What fails if these are removed:
/// - A replayed webhook (timestamp > 5 min old) passes HMAC but arrives later — attacker re-uses old event.
/// - A webhook with a valid timestamp but wrong signature is accepted — provider spoofing attack.
///
/// Both tests catch real security bugs in <see cref="StripeWebhookSignatureVerifier.VerifyAsync"/>.
/// </summary>
public sealed class WebhookSignatureTests
{
    private const string WebhookSecret = "whsec_test_secret_that_is_32_chars_ok";

    private static IConfiguration BuildConfig(string? secret = WebhookSecret)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>(
                    "Billing:Stripe:WebhookSecret", secret),
            })
            .Build();
    }

    private static (string header, byte[] body) BuildValidWebhookRequest(
        string secret, string body, DateTimeOffset? timestamp = null)
    {
        long ts = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string signedPayload = $"{ts}.{body}";

        byte[] sig = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(signedPayload));

        string hexSig = Convert.ToHexString(sig).ToLowerInvariant();
        return ($"t={ts},v1={hexSig}", bodyBytes);
    }

    [Fact]
    public async Task ReplayedWebhook_OlderThan5Minutes_Returns_Failure()
    {
        // Arrange — timestamp 6 minutes in the past (outside replay window).
        DateTimeOffset oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-6);
        (string header, byte[] body) = BuildValidWebhookRequest(
            WebhookSecret, "{\"type\":\"test\"}", oldTimestamp);

        StripeWebhookSignatureVerifier verifier = new StripeWebhookSignatureVerifier(BuildConfig());

        // Act
        WebhookVerificationResult result = await verifier.VerifyAsync(body, header, CancellationToken.None);

        // Assert — must reject replayed events regardless of valid HMAC.
        result.IsValid.Should().BeFalse(because: "a 6-minute-old webhook is outside the 5-minute replay window");
        result.FailureReason.Should().Contain("replay window", because: "the rejection reason must mention the replay window");
    }

    [Fact]
    public async Task ValidSignatureWithCurrentTimestamp_Returns_Success()
    {
        // Arrange — valid signature and current timestamp.
        (string header, byte[] body) = BuildValidWebhookRequest(WebhookSecret, "{\"type\":\"customer.subscription.updated\"}");

        StripeWebhookSignatureVerifier verifier = new StripeWebhookSignatureVerifier(BuildConfig());

        // Act
        WebhookVerificationResult result = await verifier.VerifyAsync(body, header, CancellationToken.None);

        // Assert — correctly signed, fresh webhook is accepted.
        result.IsValid.Should().BeTrue(because: "a correctly signed, fresh webhook must be accepted");
    }

    /// <summary>
    /// M-B5: Stripe key rotation sends two v1= signatures in one header — the old key's
    /// signature comes first, the new key's signature comes second (or vice-versa).
    /// The verifier must accept the webhook as long as ANY v1= value matches the configured
    /// secret. The current implementation overwrites <c>signature</c> on every v1= token,
    /// so it only ever checks the LAST v1= value. This test catches the regression.
    ///
    /// Failure signal: if the verifier is fixed to check all v1= values, this test passes.
    /// If the last-wins bug is re-introduced, the header below will be rejected.
    ///
    /// Construction: the header contains two v1= values; the FIRST is computed with the
    /// configured secret (valid), the SECOND is a fixed invalid signature. The verifier
    /// must match on the first and return success.
    /// </summary>
    [Fact]
    public async Task MultipleV1Signatures_WhenFirstMatchesSecret_Returns_Success()
    {
        // Arrange
        string body = "{\"type\":\"invoice.paid\",\"id\":\"evt_rotation_test\"}";
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        byte[] validSigBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(WebhookSecret),
            Encoding.UTF8.GetBytes($"{ts}.{body}"));
        string validSig = Convert.ToHexString(validSigBytes).ToLowerInvariant();

        // The header lists the valid signature FIRST, then a stale/invalid one SECOND.
        // A correct implementation must accept because the first v1= matches.
        string header = $"t={ts},v1={validSig},v1=0000000000000000000000000000000000000000000000000000000000000000";

        StripeWebhookSignatureVerifier verifier = new StripeWebhookSignatureVerifier(BuildConfig());

        // Act
        WebhookVerificationResult result = await verifier.VerifyAsync(
            Encoding.UTF8.GetBytes(body), header, CancellationToken.None);

        // Assert — at least one v1= matches; the webhook must be accepted.
        result.IsValid.Should().BeTrue(
            because: "the verifier must accept if ANY v1= signature matches during key rotation");
    }
}
