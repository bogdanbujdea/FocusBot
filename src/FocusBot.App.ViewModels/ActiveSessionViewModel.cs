using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for displaying an active (in-progress) focus session with a live elapsed timer.
/// Manages pause/resume/end actions via ISessionCoordinator.
/// </summary>
public partial class ActiveSessionViewModel : ObservableObject, IDisposable
{
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ISessionCoordinator _coordinator;
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
    [NotifyPropertyChangedFor(nameof(PauseResumeLabel))]
    private bool _isPaused;

    [ObservableProperty]
    private string _elapsedDisplay = "00:00:00";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseOrResumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private SessionStartState _state = SessionStartState.Idle;

    public string PauseResumeLabel => IsPaused ? "Resume" : "Pause";

    public ActiveSessionViewModel(IUIThreadDispatcher dispatcher, ISessionCoordinator coordinator)
    {
        _dispatcher = dispatcher;
        _coordinator = coordinator;
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += OnTimerElapsed;
        _coordinator.StateChanged += OnCoordinatorStateChanged;
    }

    /// <summary>
    /// Loads the view model from an API session response and starts the timer.
    /// </summary>
    public async Task LoadAsync(ApiSessionResponse session)
    {
        ApplySession(session);
        UpdateElapsedDisplay();
        StartTimerIfNotPaused();
        await Task.CompletedTask;
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

    private void OnCoordinatorStateChanged(SessionState state, SessionChangeType changeType)
    {
        _ = _dispatcher.RunOnUIThreadAsync(() =>
        {
            if (state.HasActiveSession && state.ActiveSession is not null)
            {
                ApplySession(state.ActiveSession);
            }

            if (state.HasError)
            {
                State = SessionStartState.Error(state.ErrorMessage ?? "Unknown error");
            }
            else
            {
                State = SessionStartState.Idle;
            }

            return Task.CompletedTask;
        });
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
        if (IsPaused)
        {
            await _coordinator.ResumeAsync();
        }
        else
        {
            await _coordinator.PauseAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteSessionCommand))]
    private async Task StopAsync()
    {
        await _coordinator.StopAsync();
    }

    [RelayCommand]
    private void ClearError()
    {
        _coordinator.ClearError();
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
            _coordinator.StateChanged -= OnCoordinatorStateChanged;
            _timer.Stop();
            _timer.Elapsed -= OnTimerElapsed;
            _timer.Dispose();
        }

        _disposed = true;
    }
}
