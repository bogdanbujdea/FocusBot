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
    Task<AlignmentResult?> ClassifyAlignmentAsync(
        string taskDescription,
        string processName,
        string windowTitle,
        CancellationToken ct = default);
}
