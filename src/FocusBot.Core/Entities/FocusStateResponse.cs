namespace FocusBot.Core.Entities;

/// <summary>
/// Focus state response sent back to the Chrome extension for overlay display.
/// </summary>
public class FocusStateResponse
{
    public string Status { get; set; } = "unknown";
    public string? TaskName { get; set; }
    public string? Reason { get; set; }
    public long SessionElapsedSeconds { get; set; }
    public bool Connected { get; set; } = true;
}
