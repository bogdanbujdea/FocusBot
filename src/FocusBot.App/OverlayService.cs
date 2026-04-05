using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;

namespace FocusBot.App;

/// <summary>
/// Controls the floating focus overlay window visibility and state.
/// Subscribes to session coordinator and classification events to update overlay display.
/// </summary>
public sealed class OverlayService : IOverlayService
{
    private readonly OverlaySettingsViewModel _overlaySettings;
    private readonly ISessionCoordinator _sessionCoordinator;
    private readonly IForegroundClassificationCoordinator _classificationCoordinator;
    private readonly INavigationService _navigationService;
    private readonly IUIThreadDispatcher _dispatcher;

    private FocusOverlayWindow? _overlay;
    private bool _isInitialized;
    private bool _disposed;

    private int _currentScore;
    private FocusStatus _currentStatus = FocusStatus.Neutral;

    public OverlayService(
        OverlaySettingsViewModel overlaySettings,
        ISessionCoordinator sessionCoordinator,
        IForegroundClassificationCoordinator classificationCoordinator,
        INavigationService navigationService,
        IUIThreadDispatcher dispatcher)
    {
        _overlaySettings = overlaySettings;
        _sessionCoordinator = sessionCoordinator;
        _classificationCoordinator = classificationCoordinator;
        _navigationService = navigationService;
        _dispatcher = dispatcher;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;

        _overlay = new FocusOverlayWindow(_navigationService);

        _overlaySettings.OverlayVisibilityChanged += OnOverlayVisibilityChanged;
        _sessionCoordinator.StateChanged += OnSessionStateChanged;
        _classificationCoordinator.ClassificationChanged += OnClassificationChanged;

        UpdateOverlayState();

        if (_overlaySettings.IsOverlayEnabled)
        {
            Show();
        }
    }

    public void Show()
    {
        if (_disposed || _overlay is null)
            return;

        _overlay.Show();
    }

    public void Hide()
    {
        if (_disposed || _overlay is null)
            return;

        _overlay.Hide();
    }

    private void OnOverlayVisibilityChanged(object? sender, bool enabled)
    {
        if (_disposed)
            return;

        _ = _dispatcher.RunOnUIThreadAsync(() =>
        {
            if (enabled)
                Show();
            else
                Hide();
            return Task.CompletedTask;
        });
    }

    private void OnSessionStateChanged(SessionState state, SessionChangeType changeType)
    {
        if (_disposed)
            return;

        _ = _dispatcher.RunOnUIThreadAsync(() =>
        {
            if (changeType == SessionChangeType.Stopped)
            {
                _currentScore = 0;
                _currentStatus = FocusStatus.Neutral;
            }

            UpdateOverlayState();
            return Task.CompletedTask;
        });
    }

    private void OnClassificationChanged(ClassificationStatus status)
    {
        if (_disposed)
            return;

        _currentScore = status.Score * 10;
        _currentStatus = status.Label switch
        {
            "Focused" => FocusStatus.Focused,
            "Distracted" => FocusStatus.Distracted,
            _ => FocusStatus.Neutral,
        };

        _ = _dispatcher.RunOnUIThreadAsync(() =>
        {
            UpdateOverlayState();
            return Task.CompletedTask;
        });
    }

    private void UpdateOverlayState()
    {
        if (_disposed || _overlay is null)
            return;

        var state = _sessionCoordinator.CurrentState;
        var hasActiveTask = state.HasActiveSession;
        var isPaused = state.ActiveSession?.IsPaused ?? false;

        _overlay.UpdateState(
            hasActiveTask: hasActiveTask,
            focusScorePercent: _currentScore,
            status: _currentStatus,
            isTaskPaused: isPaused,
            isLoading: false,
            hasError: state.HasError,
            tooltipText: hasActiveTask ? state.ActiveSession?.SessionTitle ?? "Focus Session" : "No active session"
        );
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _overlaySettings.OverlayVisibilityChanged -= OnOverlayVisibilityChanged;
        _sessionCoordinator.StateChanged -= OnSessionStateChanged;
        _classificationCoordinator.ClassificationChanged -= OnClassificationChanged;

        _overlay?.Dispose();
        _overlay = null;
    }
}
