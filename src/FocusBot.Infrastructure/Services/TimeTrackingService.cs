using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Fires a Tick event every second. Raises Tick on the SynchronizationContext
/// captured when Start() is called so UI can bind without marshalling.
/// </summary>
public sealed class TimeTrackingService : ITimeTrackingService
{
    private System.Timers.Timer? _timer;
    private SynchronizationContext? _syncContext;
    private const int IntervalMs = 1000;

    public event EventHandler? Tick;

    public bool IsRunning => _timer != null;

    public void Start()
    {
        Stop();
        _syncContext = SynchronizationContext.Current;
        _timer = new System.Timers.Timer(IntervalMs);
        _timer.Elapsed += OnElapsed;
        _timer.AutoReset = true;
        _timer.Start();
    }

    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Elapsed -= OnElapsed;
            _timer.Dispose();
            _timer = null;
        }
    }

    private void OnElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var handler = Tick;
        if (handler == null) return;

        if (_syncContext != null)
            _syncContext.Post(_ => handler(this, EventArgs.Empty), null);
        else
            handler(this, EventArgs.Empty);
    }
}
