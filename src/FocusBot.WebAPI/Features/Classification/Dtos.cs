namespace FocusBot.WebAPI.Features.Classification;

/// <summary>
/// Request body for the POST /classify endpoint.
/// </summary>
public sealed record ClassifyRequest(
    string TaskText,
    string? TaskHints,
    string? ProcessName,
    string? WindowTitle,
    string? Url,
    string? PageTitle,
    string? ProviderId,
    string? ModelId);

/// <summary>
/// Response body returned by the POST /classify endpoint.
/// </summary>
public sealed record ClassifyResponse(int Score, string Reason, bool Cached);
