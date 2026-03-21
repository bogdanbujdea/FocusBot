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

/// <summary>
/// Request body for the POST /classify/validate-key endpoint.
/// </summary>
public sealed record ValidateKeyRequest(string ProviderId, string ModelId, string ApiKey);

/// <summary>
/// Response for the validate-key endpoint. Valid=true means the key works.
/// Error codes: invalid_key, rate_limited, provider_unavailable.
/// </summary>
public sealed record ValidateKeyResponse(bool Valid, string? Error);

/// <summary>Structured error codes returned by the classification provider.</summary>
public enum ClassificationErrorCode
{
    InvalidKey,
    RateLimited,
    ProviderUnavailable,
}

/// <summary>Thrown when the LLM provider returns a structured error (bad key, rate limit, etc.).</summary>
public sealed class ClassificationProviderException(ClassificationErrorCode code, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public ClassificationErrorCode Code { get; } = code;
}
