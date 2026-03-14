namespace FocusBot.Core.Entities;

/// <summary>
/// Represents a browser activity event sent from the Chrome extension companion.
/// </summary>
public class BrowserActivityEvent
{
    public string EventType { get; set; } = string.Empty;
    public string Browser { get; set; } = "chrome";
    public string FullUrl { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int TabId { get; set; }
    public int WindowId { get; set; }
    public string OccurredAtUtc { get; set; } = string.Empty;
}
