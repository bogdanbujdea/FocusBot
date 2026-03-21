using CSharpFunctionalExtensions;
using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Classifies the alignment of the current foreground window with the user's active task.
/// Checks the local SQLite cache first; calls the backend API on a cache miss.
/// </summary>
public interface IClassificationService
{
    /// <summary>
    /// Returns an alignment result for the given window context and task.
    /// </summary>
    Task<Result<AlignmentResult>> ClassifyAsync(
        string processName,
        string windowTitle,
        string taskText,
        string? taskHints,
        CancellationToken ct = default);
}
