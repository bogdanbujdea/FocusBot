namespace FocusBot.Core.Interfaces;

/// <summary>
/// Dispatches work to the UI thread. Required for Store APIs that must run on the UI thread (e.g. RequestPurchaseAsync).
/// </summary>
public interface IUIThreadDispatcher
{
    /// <summary>
    /// Runs the given async function on the UI thread.
    /// </summary>
    Task RunOnUIThreadAsync(Func<Task> func);
}
