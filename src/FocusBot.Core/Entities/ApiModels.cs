using FocusBot.WebAPI.Data.Entities;

namespace FocusBot.Core.Entities;

/// <summary>Payload sent to POST /sessions.</summary>
public sealed record StartSessionPayload(string SessionTitle, string? SessionContext);

/// <summary>Payload sent to POST /sessions/{id}/end.</summary>
public sealed record EndSessionPayload(
    int FocusScorePercent,
    long FocusedSeconds,
    long DistractedSeconds,
    int DistractionCount,
    int ContextSwitchCount,
    string? TopDistractingApps,
    string? TopAlignedApps
);

/// <summary>Payload sent to POST /classify. Includes optional BYOK provider/model selection.</summary>
public sealed record ClassifyPayload(
    string SessionTitle,
    string? SessionContext,
    string? ProcessName,
    string? WindowTitle,
    string? ProviderId,
    string? ModelId
);

/// <summary>Payload sent to POST /classify/validate-key.</summary>
public sealed record ValidateKeyPayload(string ProviderId, string ModelId, string ApiKey);

/// <summary>Response from POST /sessions and GET /sessions/*.</summary>
public sealed record ApiSessionResponse(
    Guid Id,
    string SessionTitle,
    string? SessionContext,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc
);

/// <summary>Response from POST /classify.</summary>
public sealed record ApiClassifyResponse(int Score, string Reason, bool Cached);

/// <summary>Response from GET /auth/me.</summary>
public sealed record ApiMeResponse(
    Guid UserId,
    string Email,
    string SubscriptionStatus,
    PlanType PlanType,
    DateTime CreatedAtUtc,
    DateTime SubscriptionEndDate
);

/// <summary>Response from GET /subscriptions/status.</summary>
public sealed record ApiSubscriptionStatus(
    string Status,
    int PlanType,
    DateTime? TrialEndsAt,
    DateTime? CurrentPeriodEndsAt,
    DateTime? NextBilledAtUtc = null
);

/// <summary>Payload sent to POST /clients.</summary>
public sealed record RegisterClientRequest(
    ClientType ClientType,
    ClientHost Host,
    string Name,
    string Fingerprint,
    string? AppVersion,
    string? Platform
);

/// <summary>Response from POST /clients.</summary>
/// <remarks>ClientType and Host are integer enums serialized as numbers by the API.</remarks>
public sealed record ApiClientResponse(
    Guid Id,
    int ClientType,
    int Host,
    string Name,
    string Fingerprint,
    string? AppVersion,
    string? Platform,
    string? IpAddress,
    DateTime LastSeenAtUtc,
    DateTime CreatedAtUtc,
    bool IsOnline
);

/// <summary>Response from POST /classify/validate-key.</summary>
public sealed record ApiValidateKeyResponse(bool Valid, string? Error);
