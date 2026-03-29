using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FocusBot.WebAPI.Features.Subscriptions;

/// <summary>
/// Verifies Paddle Billing webhook signatures (Paddle-Signature: ts=...;h1=...).
/// </summary>
public static class PaddleWebhookVerifier
{
    private const int MaxSkewSeconds = 300;

    /// <summary>
    /// Returns true when the signature matches the raw body, or when <paramref name="webhookSecret"/> is null/empty (dev only).
    /// </summary>
    public static bool TryVerify(
        string rawBody,
        string? signatureHeader,
        string? webhookSecret,
        out string? error
    )
    {
        error = null;

        if (string.IsNullOrEmpty(webhookSecret))
        {
            error = "Webhook secret is not configured; rejecting request.";
            return false;
        }

        if (string.IsNullOrEmpty(signatureHeader))
        {
            error = "Missing Paddle-Signature header.";
            return false;
        }

        string? ts = null;
        string? h1 = null;

        foreach (var part in signatureHeader.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (key.Equals("ts", StringComparison.OrdinalIgnoreCase))
                ts = value;
            else if (key.Equals("h1", StringComparison.OrdinalIgnoreCase))
                h1 = value;
        }

        if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(h1))
        {
            error = "Paddle-Signature header must include ts and h1.";
            return false;
        }

        if (!long.TryParse(ts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTs))
        {
            error = "Invalid ts in Paddle-Signature.";
            return false;
        }

        var eventTime = DateTimeOffset.FromUnixTimeSeconds(unixTs);
        var skew = DateTimeOffset.UtcNow - eventTime;
        if (skew.Duration() > TimeSpan.FromSeconds(MaxSkewSeconds))
        {
            error = "Webhook timestamp outside allowed window.";
            return false;
        }

        var signedPayload = $"{ts}:{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedHex),
                Encoding.UTF8.GetBytes(h1.ToLowerInvariant())))
        {
            error = "Signature mismatch.";
            return false;
        }

        return true;
    }
}
