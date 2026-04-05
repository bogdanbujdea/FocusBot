using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for displaying an active (in-progress) focus session with a live elapsed timer.
/// Manages pause/resume/end actions via IFocusSessionControlService.
/// </summary>
public partial class ActiveSessionViewModel : ObservableObject, IDisposable
{
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly IFocusSessionControlService _sessionControl;
    private readonly System.Timers.Timer _timer;
    private bool _disposed;

    private Guid _sessionId;
    private DateTime? _pausedAtUtc;
    private long _totalPausedSeconds;

    [ObservableProperty]
    private string _sessionTitle = string.Empty;

    [ObservableProperty]
    private DateTime _startedAtUtc;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _elapsedDisplay = "00:00:00";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseOrResumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private SessionStartState _state = SessionStartState.Idle;

    public Action? OnSessionEnded { get; set; }

    public string PauseResumeLabel => IsPaused ? "Resume" : "Pause";

    public ActiveSessionViewModel(IUIThreadDispatcher dispatcher, IFocusSessionControlService sessionControl)
    {
        _dispatcher = dispatcher;
        _sessionControl = sessionControl;
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += OnTimerElapsed;
    }

    /// <summary>
    /// Populates the view model from an API session response and starts the timer.
    /// </summary>
    public void SetSession(ApiSessionResponse session)
    {
        ApplySession(session);
        UpdateElapsedDisplay();
        StartTimerIfNotPaused();
    }

    /// <summary>
    /// Clears the session data and stops the timer.
    /// </summary>
    public void Clear()
    {
        StopTimer();
        SessionTitle = string.Empty;
        StartedAtUtc = default;
        _sessionId = Guid.Empty;
        _pausedAtUtc = null;
        _totalPausedSeconds = 0;
        IsPaused = false;
        ElapsedDisplay = "00:00:00";
        State = SessionStartState.Idle;
    }

    private void ApplySession(ApiSessionResponse session)
    {
        _sessionId = session.Id;
        SessionTitle = session.SessionTitle;
        StartedAtUtc = session.StartedAtUtc;
        _pausedAtUtc = session.PausedAtUtc;
        _totalPausedSeconds = session.TotalPausedSeconds;
        IsPaused = session.IsPaused;
        UpdateElapsedDisplay();
        ManagedTimer();
    }

    private void ManagedTimer()
    {
        if (IsPaused)
            StopTimer();
        else
            StartTimerIfNotPaused();
    }

    private void StartTimerIfNotPaused()
    {
        if (!IsPaused)
            _timer.Start();
    }

    private void StopTimer()
    {
        _timer.Stop();
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _ = _dispatcher.RunOnUIThreadAsync(() =>
        {
            UpdateElapsedDisplay();
            return Task.CompletedTask;
        });
    }

    private void UpdateElapsedDisplay()
    {
        var seconds = ComputeActiveSeconds();
        ElapsedDisplay = FormatDuration(seconds);
    }

    private int ComputeActiveSeconds()
    {
        var endRef = IsPaused && _pausedAtUtc.HasValue
            ? _pausedAtUtc.Value
            : DateTime.UtcNow;
        var wallSeconds = (int)(endRef - StartedAtUtc).TotalSeconds;
        return Math.Max(0, wallSeconds - (int)_totalPausedSeconds);
    }

    private static string FormatDuration(int totalSeconds)
    {
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    private bool CanExecuteSessionCommand => !State.IsBusy && _sessionId != Guid.Empty;

    [RelayCommand(CanExecute = nameof(CanExecuteSessionCommand))]
    private async Task PauseOrResumeAsync()
    {
        State = SessionStartState.Loading;
        var result = await _sessionControl.TogglePauseAsync(_sessionId, IsPaused);
        if (result.IsSuccess && result.Value is not null)
        {
            ApplySession(result.Value);
            State = SessionStartState.Idle;
        }
        else
        {
            State = SessionStartState.Error(result.ErrorMessage ?? "Failed to toggle pause");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteSessionCommand))]
    private async Task StopAsync()
    {
        State = SessionStartState.Loading;
        var result = await _sessionControl.EndWithPlaceholderMetricsAsync(_sessionId);
        if (result.IsSuccess)
        {
            OnSessionEnded?.Invoke();
        }
        else
        {
            State = SessionStartState.Error(result.ErrorMessage ?? "Failed to end session");
        }
    }

    [RelayCommand]
    private void ClearError()
    {
        State = SessionStartState.Idle;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _timer.Stop();
            _timer.Elapsed -= OnTimerElapsed;
            _timer.Dispose();
        }

        _disposed = true;
    }
}
