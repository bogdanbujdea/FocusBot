using FocusBot.Core.Entities;
using FocusBot.Core.Events;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Detects distraction episodes based on sampled focus status over time.
/// </summary>
public interface IDistractionDetectorService
{
    /// <summary>
    /// Records a focus status sample for the current task and window.
    /// Call this once per second while a task is in progress.
    /// </summary>
    Task OnSampleAsync(
        string taskId,
        FocusStatus status,
        string processName,
        string windowTitle,
        DateTime sampleTimeUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised whenever a new distraction event is detected and persisted.
    /// </summary>
    event EventHandler<DistractionEvent>? DistractionEventCreated;
}

