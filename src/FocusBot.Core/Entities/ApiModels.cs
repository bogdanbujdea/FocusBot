namespace FocusBot.Core.Entities;

/// <summary>Payload sent to POST /sessions.</summary>
public sealed record StartSessionPayload(
    string SessionTitle,
    string? SessionContext,
    Guid? DeviceId
);

/// <summary>Payload sent to POST /sessions/{id}/end.</summary>
public sealed record EndSessionPayload(
    int FocusScorePercent,
    long FocusedSeconds,
    long DistractedSeconds,
    int DistractionCount,
    int ContextSwitchCount,
    string? TopDistractingApps,
    string? TopAlignedApps,
    Guid? DeviceId
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
    Guid? DeviceId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc
);

/// <summary>Response from POST /classify.</summary>
public sealed record ApiClassifyResponse(int Score, string Reason, bool Cached);

/// <summary>Response from GET /subscriptions/status.</summary>
public sealed record ApiSubscriptionStatus(
    string Status,
    int PlanType,
    DateTime? TrialEndsAt,
    DateTime? CurrentPeriodEndsAt
);

/// <summary>Response from POST /devices and PUT /devices/{id}/heartbeat.</summary>
/// <remarks>DeviceType is an integer enum: 1 = Desktop, 2 = Extension (serialized as a number by the API).</remarks>
public sealed record ApiDeviceResponse(
    Guid Id,
    int DeviceType,
    string Name,
    string Fingerprint,
    string? AppVersion,
    string? Platform,
    DateTime LastSeenAtUtc,
    DateTime CreatedAtUtc,
    bool IsOnline
);

/// <summary>Response from POST /classify/validate-key.</summary>
public sealed record ApiValidateKeyResponse(bool Valid, string? Error);
