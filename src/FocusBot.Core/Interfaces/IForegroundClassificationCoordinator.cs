using FocusBot.Core.Entities;

namespace FocusBot.Core.Interfaces;

/// <summary>
/// Coordinates foreground window change detection with the classification API.
/// Called by <see cref="ISessionCoordinator"/> when session state changes.
/// </summary>
public interface IForegroundClassificationCoordinator
{
    /// <summary>
    /// Raised when a classification result is received.
    /// </summary>
    event Action<ClassificationStatus>? ClassificationChanged;

    /// <summary>
    /// Start classifying foreground window changes for the given session.
    /// Subscribes to window change events and calls the classification API.
    /// </summary>
    void Start(string sessionTitle, string? sessionContext);

    /// <summary>
    /// Stop classifying foreground window changes.
    /// Unsubscribes from window change events.
    /// </summary>
    void Stop();
}
