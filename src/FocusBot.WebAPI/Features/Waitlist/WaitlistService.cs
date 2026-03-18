using System.Net;
using System.Net.Http.Json;

namespace FocusBot.WebAPI.Features.Waitlist;

public sealed class WaitlistService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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

        if (response.StatusCode is HttpStatusCode.UnprocessableEntity)
        {
            throw new InvalidOperationException("MailerLite rejected the subscriber payload.");
        }

        var payload = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"MailerLite signup failed with {(int)response.StatusCode}: {payload}");
    }

    private sealed record MailerLiteUpsertSubscriberRequest(
        string email,
        string[] groups,
        string? ip_address);
}

