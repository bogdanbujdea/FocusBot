namespace FocusBot.Core.Entities;

/// <summary>
/// Outcome of ending a focus session locally and syncing with the backend.
/// </summary>
public sealed class SessionEndResult
{
    public required SessionSummary Summary { get; init; }

    /// <summary>
    /// User-facing message when the backend end-session call failed; local data was still saved.
    /// </summary>
    public string? ApiErrorMessage { get; init; }
}
