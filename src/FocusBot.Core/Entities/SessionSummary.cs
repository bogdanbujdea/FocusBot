namespace FocusBot.Core.Entities;

/// <summary>
/// Aggregate summary of a focus session, computed locally by <see cref="FocusBot.Core.Interfaces.ILocalSessionTracker"/>.
/// Submitted to the backend when the session ends.
/// </summary>
public sealed class SessionSummary
{
    /// <summary>Time-weighted alignment percentage (0–100).</summary>
    public int FocusScorePercent { get; init; }

    /// <summary>Total seconds spent in an aligned state.</summary>
    public long FocusedSeconds { get; init; }

    /// <summary>Total seconds spent in a distracted (not-aligned) state.</summary>
    public long DistractedSeconds { get; init; }

    /// <summary>Number of times the user transitioned from aligned to distracted.</summary>
    public int DistractionCount { get; init; }

    /// <summary>Number of context switches between different applications.</summary>
    public int ContextSwitchCount { get; init; }

    /// <summary>JSON-serialised list of top distracting apps sorted by distracted seconds.</summary>
    public string? TopDistractingApps { get; init; }

    /// <summary>JSON-serialised list of top aligned apps sorted by focused seconds.</summary>
    public string? TopAlignedApps { get; init; }
}
