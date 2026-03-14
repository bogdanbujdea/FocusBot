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

    [JsonPropertyName("hasActiveTask")]
    public bool HasActiveTask { get; set; }

    [JsonPropertyName("taskId")]
    public string? TaskId { get; set; }

    [JsonPropertyName("taskText")]
    public string? TaskText { get; set; }

    [JsonPropertyName("taskHints")]
    public string? TaskHints { get; set; }
}

public class TaskStartedPayload
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("taskText")]
    public string TaskText { get; set; } = string.Empty;

    [JsonPropertyName("taskHints")]
    public string? TaskHints { get; set; }
}

public class TaskEndedPayload
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;
}

public class FocusStatusPayload
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

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

public static class IntegrationMessageTypes
{
    public const string Handshake = "HANDSHAKE";
    public const string TaskStarted = "TASK_STARTED";
    public const string TaskEnded = "TASK_ENDED";
    public const string FocusStatus = "FOCUS_STATUS";
    public const string DesktopForeground = "DESKTOP_FOREGROUND";
    public const string RequestBrowserUrl = "REQUEST_BROWSER_URL";
    public const string BrowserUrlResponse = "BROWSER_URL_RESPONSE";
}
