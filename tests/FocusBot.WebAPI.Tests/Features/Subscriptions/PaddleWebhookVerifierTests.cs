using System.Security.Cryptography;
using System.Text;
using FocusBot.WebAPI.Features.Subscriptions;

namespace FocusBot.WebAPI.Tests.Features.Subscriptions;

public class PaddleWebhookVerifierTests
{
    [Fact]
    public void TryVerify_ReturnsTrue_WhenSecretEmpty()
    {
        var ok = PaddleWebhookVerifier.TryVerify("{}", null, "", out var err);
        ok.Should().BeTrue();
        err.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryVerify_ReturnsFalse_WhenHeaderMissing()
    {
        var ok = PaddleWebhookVerifier.TryVerify("{}", null, "secret", out var err);
        ok.Should().BeFalse();
        err.Should().Contain("Missing");
    }

    [Fact]
    public void TryVerify_ReturnsTrue_ForValidSignature()
    {
        const string secret = "pdl_ntfset_test_secret";
        const string body = """{"event_type":"test"}""";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signed = $"{ts}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var h1 = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
        var header = $"ts={ts};h1={h1}";

        var ok = PaddleWebhookVerifier.TryVerify(body, header, secret, out var err);
        ok.Should().BeTrue();
    }

    [Fact]
    public void TryVerify_ReturnsFalse_ForTamperedBody()
    {
        const string secret = "pdl_ntfset_test_secret";
        const string body = """{"event_type":"test"}""";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signed = $"{ts}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var h1 = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
        var header = $"ts={ts};h1={h1}";

        var ok = PaddleWebhookVerifier.TryVerify(body + "x", header, secret, out _);
        ok.Should().BeFalse();
    }

    [Fact]
    public void TryVerify_ReturnsFalse_ForExpiredTimestamp()
    {
        const string secret = "pdl_ntfset_test_secret";
        const string body = "{}";
        var ts = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString();
        var signed = $"{ts}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var h1 = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
        var header = $"ts={ts};h1={h1}";

        var ok = PaddleWebhookVerifier.TryVerify(body, header, secret, out var err);
        ok.Should().BeFalse();
        err.Should().Contain("window");
    }
}
