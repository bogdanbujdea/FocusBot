namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service for managing the 24-hour free trial period.
/// </summary>
public interface ITrialService
{
    /// <summary>
    /// Gets whether the trial has been started.
    /// </summary>
    Task<bool> HasTrialStartedAsync();

    /// <summary>
    /// Gets whether the trial is currently active (started and not expired).
    /// </summary>
    Task<bool> IsTrialActiveAsync();

    /// <summary>
    /// Gets whether the trial has expired.
    /// </summary>
    Task<bool> IsTrialExpiredAsync();

    /// <summary>
    /// Gets the trial end time, or null if trial hasn't started.
    /// </summary>
    Task<DateTime?> GetTrialEndTimeAsync();

    /// <summary>
    /// Gets the remaining time in the trial, or TimeSpan.Zero if expired or not started.
    /// </summary>
    Task<TimeSpan> GetTrialTimeRemainingAsync();

    /// <summary>
    /// Starts the trial period. Does nothing if trial has already been started.
    /// </summary>
    Task StartTrialAsync();
}
