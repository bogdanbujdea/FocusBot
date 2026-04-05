namespace FocusBot.Core.Interfaces;

/// <summary>
/// Controls the floating focus overlay window visibility and state.
/// Coordinates between settings, session state, and classification results.
/// </summary>
public interface IOverlayService : IDisposable
{
    /// <summary>
    /// Initialize the overlay and subscribe to state change events.
    /// Should be called after UI thread dispatcher is available.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Show the overlay window if the setting is enabled.
    /// </summary>
    void Show();

    /// <summary>
    /// Hide the overlay window.
    /// </summary>
    void Hide();
}
