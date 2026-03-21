using System.Text.Json;
using System.Text.Json.Serialization;

namespace FocusBot.Core.DTOs;

/// <summary>
/// Envelope for all integration messages exchanged between app and extension over WebSocket.
/// </summary>
public class IntegrationEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }
}

public class HandshakePayload
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("hasActiveSession")]
    public bool HasActiveSession { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("sessionTitle")]
    public string? SessionTitle { get; set; }

    [JsonPropertyName("sessionContext")]
    public string? SessionContext { get; set; }

    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; set; }
}

public class SessionStartedPayload
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("sessionTitle")]
    public string SessionTitle { get; set; } = string.Empty;

    [JsonPropertyName("sessionContext")]
    public string? SessionContext { get; set; }

    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; set; }
}

public class SessionEndedPayload
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;
}

public class FocusStatusPayload
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("classification")]
    public string Classification { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("focusScorePercent")]
    public int FocusScorePercent { get; set; }

    [JsonPropertyName("contextType")]
    public string ContextType { get; set; } = string.Empty;

    [JsonPropertyName("contextTitle")]
    public string ContextTitle { get; set; } = string.Empty;
}

public class DesktopForegroundPayload
{
    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonPropertyName("windowTitle")]
    public string WindowTitle { get; set; } = string.Empty;
}

public class RequestBrowserUrlPayload
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;
}

public class BrowserUrlResponsePayload
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public class BrowserContextPayload
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public static class IntegrationMessageTypes
{
    public const string Handshake = "HANDSHAKE";
    public const string SessionStarted = "SESSION_STARTED";
    public const string SessionEnded = "SESSION_ENDED";
    public const string FocusStatus = "FOCUS_STATUS";
    public const string DesktopForeground = "DESKTOP_FOREGROUND";
    public const string BrowserContext = "BROWSER_CONTEXT";
}
