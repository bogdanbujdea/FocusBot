using System.Runtime.InteropServices;
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Monitors user input activity via GetLastInputInfo and raises events
/// when the user transitions between idle and active states.
/// Events are raised on the SynchronizationContext captured when Start() is called.
/// </summary>
public sealed class IdleDetectionService : IIdleDetectionService
{
    private Timer? _timer;
    private SynchronizationContext? _syncContext;
    private bool _isUserIdle;
    private const int PollIntervalMs = 10_000;

    public event EventHandler? UserBecameIdle;
    public event EventHandler? UserBecameActive;

    public TimeSpan IdleThreshold { get; set; } = TimeSpan.FromMinutes(5);

    public bool IsUserIdle => _isUserIdle;

    public void Start()
    {
        Stop();
        _syncContext = SynchronizationContext.Current;
        _isUserIdle = false;
        _timer = new Timer(CheckIdleState, null, PollIntervalMs, PollIntervalMs);
    }

    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Dispose();
            _timer = null;
        }
        _isUserIdle = false;
    }

    private void CheckIdleState(object? state)
    {
        var idleTime = GetIdleTime();
        var wasIdle = _isUserIdle;
        var isNowIdle = idleTime >= IdleThreshold;

        if (wasIdle == isNowIdle)
            return;

        _isUserIdle = isNowIdle;

        if (isNowIdle)
            RaiseEvent(UserBecameIdle);
        else
            RaiseEvent(UserBecameActive);
    }

    private void RaiseEvent(EventHandler? handler)
    {
        if (handler == null)
            return;

        if (_syncContext != null)
            _syncContext.Post(_ => handler(this, EventArgs.Empty), null);
        else
            handler(this, EventArgs.Empty);
    }

    private static TimeSpan GetIdleTime()
    {
        var lastInputInfo = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref lastInputInfo))
            return TimeSpan.Zero;

        var lastInputTick = lastInputInfo.dwTime;
        var currentTick = Environment.TickCount;

        var elapsedMs = unchecked((uint)(currentTick - (int)lastInputTick));
        return TimeSpan.FromMilliseconds(elapsedMs);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}
