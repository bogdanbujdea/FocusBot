using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service for classifying how aligned the current window is with the user's task.
/// </summary>
public interface IOpenAIService
{
    /// <summary>
    /// Classifies alignment of the given window/process with the task description.
    /// Returns null if API key is not set or on error.
    /// </summary>
    /// <param name="taskDescription">Task description.</param>
    /// <param name="taskContext">Optional alignment hints (e.g. "Outlook is work email"). Passed to the prompt.</param>
    /// <param name="processName">Current window process name.</param>
    /// <param name="windowTitle">Current window title.</param>
    Task<AlignmentResult?> ClassifyAlignmentAsync(
        string taskDescription,
        string? taskContext,
        string processName,
        string windowTitle,
        CancellationToken ct = default);
}
