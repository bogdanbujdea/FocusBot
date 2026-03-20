namespace FocusBot.Core.Entities;

/// <summary>Payload sent to POST /sessions.</summary>
public sealed record StartSessionPayload(string TaskText, string? TaskHints, Guid? DeviceId);

/// <summary>Payload sent to POST /sessions/{id}/end.</summary>
public sealed record EndSessionPayload(
    int FocusScorePercent,
    long FocusedSeconds,
    long DistractedSeconds,
    int DistractionCount,
    int ContextSwitchCount,
    string? TopDistractingApps,
    string? TopAlignedApps,
    Guid? DeviceId);

/// <summary>Payload sent to POST /classify. Includes optional BYOK provider/model selection.</summary>
public sealed record ClassifyPayload(
    string TaskText,
    string? TaskHints,
    string? ProcessName,
    string? WindowTitle,
    string? ProviderId,
    string? ModelId);

/// <summary>Payload sent to POST /classify/validate-key.</summary>
public sealed record ValidateKeyPayload(string ProviderId, string ModelId, string ApiKey);

/// <summary>Response from POST /sessions and GET /sessions/*.</summary>
public sealed record ApiSessionResponse(Guid Id, string TaskText, string? TaskHints, Guid? DeviceId, DateTime StartedAtUtc, DateTime? EndedAtUtc);

/// <summary>Response from POST /classify.</summary>
public sealed record ApiClassifyResponse(int Score, string Reason, bool Cached);

/// <summary>Response from GET /subscriptions/status.</summary>
public sealed record ApiSubscriptionStatus(string Status, int PlanType, DateTime? TrialEndsAt, DateTime? CurrentPeriodEndsAt);

/// <summary>Response from POST /devices and PUT /devices/{id}/heartbeat.</summary>
public sealed record ApiDeviceResponse(Guid Id, string DeviceType, string Name, string Fingerprint, DateTime LastSeenAtUtc);

/// <summary>Response from POST /classify/validate-key.</summary>
public sealed record ApiValidateKeyResponse(bool Valid, string? Error);
