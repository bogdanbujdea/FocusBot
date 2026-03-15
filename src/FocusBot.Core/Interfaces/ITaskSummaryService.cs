namespace FocusBot.Core.Interfaces;

/// <summary>
/// Computes and persists task summary metrics from focus segments and distraction events,
/// then cleans up raw data to minimize storage.
/// </summary>
public interface ITaskSummaryService
{
    /// <summary>
    /// Computes summary metrics for the task from segments and events,
    /// persists them to the task, then deletes the raw segments and events.
    /// </summary>
    Task ComputeAndPersistSummaryAsync(string taskId);
}
