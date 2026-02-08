namespace FocusBot.Core.Entities;

/// <summary>
/// Represents a unique window context (process name + normalized window title) for alignment cache lookups.
/// </summary>
public class WindowContext
{
    public string ContextHash { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
}
