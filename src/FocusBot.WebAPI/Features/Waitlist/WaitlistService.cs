using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace FocusBot.WebAPI.Features.Waitlist;

public sealed class WaitlistService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<WaitlistService> logger)
{
    public const string HttpClientName = "MailerLite";

    public async Task UpsertWaitlistSubscriberAsync(string normalizedEmail, HttpContext httpContext, CancellationToken ct)
    {
        var apiKey = configuration["MailerLite:ApiKey"];
        var groupId = configuration["MailerLite:WaitlistGroupId"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(groupId))
        {
            throw new InvalidOperationException(
                "MailerLite is not configured. Set MailerLite:ApiKey and MailerLite:WaitlistGroupId.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var body = new MailerLiteUpsertSubscriberRequest(
            normalizedEmail,
            [groupId],
            ipAddress);

        using var response = await client.PostAsJsonAsync("subscribers", body, cancellationToken: ct);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var payload = await response.Content.ReadAsStringAsync(ct);
        var traceId = httpContext.TraceIdentifier;
        var emailHash = ComputeSha256(normalizedEmail);
        var truncatedPayload = TruncateForLogs(payload, 1024);

        logger.LogWarning(
            "MailerLite subscribe failed with status {StatusCode}. TraceId={TraceId}, GroupId={GroupId}, EmailHash={EmailHash}, Response={ResponsePayload}",
            (int)response.StatusCode,
            traceId,
            groupId,
            emailHash,
            truncatedPayload);

        if (response.StatusCode is HttpStatusCode.UnprocessableEntity)
        {
            throw new InvalidOperationException(
                $"MailerLite rejected the subscriber payload. TraceId={traceId}, Response={truncatedPayload}");
        }

        throw new InvalidOperationException(
            $"MailerLite signup failed with {(int)response.StatusCode}. TraceId={traceId}, Response={truncatedPayload}");
    }

    private sealed record MailerLiteUpsertSubscriberRequest(
        string email,
        string[] groups,
        string? ip_address);

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string TruncateForLogs(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}

