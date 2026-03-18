using System.Text.Json.Serialization;

namespace FocusBot.WebAPI.Features.Waitlist;

public sealed record WaitlistSignupRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("company")] string? Company)
{
    public bool IsHoneypotEmpty() => string.IsNullOrWhiteSpace(Company);
}

