using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Service for classifying how aligned the current window is with the user's task.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Classifies alignment of the given window/process with the task description.
    /// Result is null when API key is not set or on error; ErrorMessage is set when the failure was due to an API/request error.
    /// </summary>
    /// <param name="taskDescription">Task description.</param>
    /// <param name="taskContext">Optional alignment hints (e.g. "Outlook is work email"). Passed to the prompt.</param>
    /// <param name="processName">Current window process name.</param>
    /// <param name="windowTitle">Current window title.</param>
    Task<ClassifyAlignmentResponse> ClassifyAlignmentAsync(
        string taskDescription,
        string? taskContext,
        string processName,
        string windowTitle,
        CancellationToken ct = default);

    /// <summary>
    /// Validates that the given API key and provider/model can be used to call the API.
    /// Makes a minimal request; does not use stored settings.
    /// </summary>
    Task<ClassifyAlignmentResponse> ValidateCredentialsAsync(
        string apiKey,
        string providerId,
        string modelId,
        CancellationToken ct = default);
}
