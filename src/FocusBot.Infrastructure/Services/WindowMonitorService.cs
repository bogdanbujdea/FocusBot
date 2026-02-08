using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Monitors the foreground window via polling. Raises ForegroundWindowChanged on the
/// SynchronizationContext captured when Start() is called so UI can bind without marshalling.
/// If no SyncContext (e.g. Start called from non-UI thread), events are raised on the timer thread.
/// </summary>
public sealed class WindowMonitorService : IWindowMonitorService
{
    private Timer? _timer;
    private SynchronizationContext? _syncContext;
    private const int PollIntervalMs = 1000;

    public event EventHandler<ForegroundWindowChangedEventArgs>? ForegroundWindowChanged;

    public void Start()
    {
        Stop();
        _syncContext = SynchronizationContext.Current;
        _timer = new Timer(Tick, null, 0, PollIntervalMs);
    }

    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Dispose();
            _timer = null;
        }

        RaiseForegroundWindowChanged(string.Empty, string.Empty);
    }

    private void Tick(object? _)
    {
        var (processName, windowTitle) = GetForegroundWindowInfo();
        RaiseForegroundWindowChanged(processName, windowTitle);
    }

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

    private static (string ProcessName, string WindowTitle) GetForegroundWindowInfo()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return (string.Empty, string.Empty);

        var title = GetWindowTitle(hwnd);
        GetWindowThreadProcessId(hwnd, out var pid);
        var processName = GetProcessName(pid);
        return (processName, title);
    }

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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
