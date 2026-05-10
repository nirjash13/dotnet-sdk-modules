using System;
using System.Security.Cryptography;
using System.Text;

namespace Webhooks.Application.Signing;

/// <summary>
/// Generates and verifies Standard Webhooks signatures.
/// Spec: https://github.com/standard-webhooks/standard-webhooks
/// Signature scheme: <c>v1,{base64(HMAC-SHA256(secret, "{msgId}.{timestamp}.{body}"))}</c>
/// Multiple signatures are space-separated to support secret rotation.
/// Replay window: 5 minutes.
/// </summary>
public static class StandardWebhooksSigner
{
    private const int ReplayWindowSeconds = 300; // 5 minutes

    /// <summary>
    /// Computes the Standard Webhooks signature for a single secret.
    /// </summary>
    /// <param name="secret">Raw HMAC secret bytes.</param>
    /// <param name="messageId">The <c>webhook-id</c> header value.</param>
    /// <param name="timestamp">The <c>webhook-timestamp</c> header value (Unix seconds).</param>
    /// <param name="body">The raw request body string.</param>
    /// <returns>A <c>v1,{base64}</c> signature fragment.</returns>
    public static string ComputeSignature(byte[] secret, string messageId, long timestamp, string body)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentNullException.ThrowIfNull(body);

        string payload = $"{messageId}.{timestamp}.{body}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        using HMACSHA256 hmac = new HMACSHA256(secret);
        byte[] hash = hmac.ComputeHash(payloadBytes);
        return $"v1,{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Builds the full <c>webhook-signature</c> header value for one or more secrets
    /// (current + optional previous for rotation).
    /// </summary>
    public static string BuildSignatureHeader(
        byte[] currentSecret,
        string messageId,
        long timestamp,
        string body,
        byte[]? previousSecret = null)
    {
        string current = ComputeSignature(currentSecret, messageId, timestamp, body);
        if (previousSecret is null)
        {
            return current;
        }

        string previous = ComputeSignature(previousSecret, messageId, timestamp, body);
        return $"{current} {previous}";
    }

    /// <summary>
    /// Verifies a received webhook. Returns <see langword="true"/> when at least one
    /// signature in <paramref name="signatureHeader"/> matches a computed signature, and
    /// the timestamp is within the replay window.
    /// </summary>
    public static bool Verify(
        byte[] secret,
        string messageId,
        string timestampHeader,
        string signatureHeader,
        string body,
        DateTimeOffset? now = null)
    {
        if (!long.TryParse(timestampHeader, out long timestamp))
        {
            return false;
        }

        DateTimeOffset msgTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        DateTimeOffset reference = now ?? DateTimeOffset.UtcNow;

        if (Math.Abs((reference - msgTime).TotalSeconds) > ReplayWindowSeconds)
        {
            return false;
        }

        string expected = ComputeSignature(secret, messageId, timestamp, body);
        string[] received = signatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (string sig in received)
        {
            if (CryptographicEquals(sig, expected))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Constant-time string comparison to prevent timing attacks.</summary>
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        byte[] aBytes = Encoding.UTF8.GetBytes(a);
        byte[] bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
