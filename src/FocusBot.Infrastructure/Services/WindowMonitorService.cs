using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Consolidated monitoring service that polls every second for:
/// - Foreground window changes
/// - Elapsed time tracking (Tick event)
/// - User idle state (via GetLastInputInfo)
/// Events are raised on the SynchronizationContext captured when Start() is called.
/// </summary>
public sealed class WindowMonitorService : IWindowMonitorService
{
    private Timer? _timer;
    private SynchronizationContext? _syncContext;
    private const int PollIntervalMs = 1000;

    private string _lastProcessName = string.Empty;
    private string _lastWindowTitle = string.Empty;
    private bool _isUserIdle;
    private int _ticksSinceLastIdleCheck;
    private const int IdleCheckIntervalTicks = 10; // Check idle state every 10 seconds

    public event EventHandler<ForegroundWindowChangedEventArgs>? ForegroundWindowChanged;
    public event EventHandler? Tick;
    public event EventHandler? UserBecameIdle;
    public event EventHandler? UserBecameActive;

    public TimeSpan IdleThreshold { get; set; } = TimeSpan.FromMinutes(5);
    public bool IsUserIdle => _isUserIdle;

    public void Start()
    {
        Stop();
        _syncContext = SynchronizationContext.Current;
        _lastProcessName = string.Empty;
        _lastWindowTitle = string.Empty;
        _isUserIdle = false;
        _ticksSinceLastIdleCheck = 0;
        _timer = new Timer(OnTimerTick, null, 0, PollIntervalMs);
    }

    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Dispose();
            _timer = null;
        }

        _isUserIdle = false;
        RaiseForegroundWindowChanged(string.Empty, string.Empty);
    }

    private void OnTimerTick(object? _)
    {
        // 1. Check foreground window
        CheckForegroundWindow();

        // 2. Raise Tick event for elapsed time tracking
        RaiseTickEvent();

        // 3. Check idle state every N ticks (every 10 seconds)
        _ticksSinceLastIdleCheck++;
        if (_ticksSinceLastIdleCheck >= IdleCheckIntervalTicks)
        {
            _ticksSinceLastIdleCheck = 0;
            CheckIdleState();
        }
    }

    private void CheckForegroundWindow()
    {
        var info = GetForegroundWindowInfo();
        if (!HasWindowChanged(info))
            return;
        _lastProcessName = info.ProcessName;
        _lastWindowTitle = info.WindowTitle;
        RaiseForegroundWindowChanged(info.ProcessName, info.WindowTitle);
    }

    private void RaiseTickEvent()
    {
        var handler = Tick;
        if (handler == null)
            return;

        if (_syncContext != null)
            _syncContext.Post(_ => handler(this, EventArgs.Empty), null);
        else
            handler(this, EventArgs.Empty);
    }

    private void CheckIdleState()
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

    private bool HasWindowChanged(ForegroundWindowInfo info) =>
        info.ProcessName != _lastProcessName || info.WindowTitle != _lastWindowTitle;

    private void RaiseForegroundWindowChanged(string processName, string windowTitle)
    {
        var args = new ForegroundWindowChangedEventArgs
        {
            ProcessName = processName,
            WindowTitle = windowTitle
        };
        var handler = ForegroundWindowChanged;
        if (handler == null) return;

        if (_syncContext != null)
            _syncContext.Post(_ => handler(this, args), null);
        else
            handler(this, args);
    }

    private static ForegroundWindowInfo GetForegroundWindowInfo()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return new ForegroundWindowInfo(string.Empty, string.Empty);

        var title = GetWindowTitle(hwnd);
        GetWindowThreadProcessId(hwnd, out var pid);
        var processName = GetProcessName(pid);
        return new ForegroundWindowInfo(processName, title);
    }

    private sealed record ForegroundWindowInfo(string ProcessName, string WindowTitle);

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        return GetWindowText(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }

    private static string GetProcessName(uint pid)
    {
        if (pid == 0) return string.Empty;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName ?? string.Empty;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
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
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}
