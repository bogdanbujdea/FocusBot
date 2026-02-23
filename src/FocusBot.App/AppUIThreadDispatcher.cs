using FocusBot.Core.Interfaces;
using Microsoft.UI.Dispatching;

namespace FocusBot.App;

/// <summary>
/// Dispatches work to the main window's UI thread. DispatcherQueue is set in OnLaunched.
/// </summary>
public class AppUIThreadDispatcher : IUIThreadDispatcher
{
    public DispatcherQueue? DispatcherQueue { get; set; }

    public Task RunOnUIThreadAsync(Func<Task> func)
    {
        if (DispatcherQueue == null)
            return func();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await func();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }
}
