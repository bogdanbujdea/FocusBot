using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for the current foreground window status bar shown during an active focus session.
/// Displays process name, window title, focus score, classification status, and manual override controls.
/// </summary>
public partial class FocusStatusViewModel : ObservableObject
{
    private readonly IFocusSessionOrchestrator _sessionOrchestrator;
    private readonly IUIThreadDispatcher? _uiDispatcher;

    public string CurrentProcessName
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string CurrentWindowTitle
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsMonitoring
    {
        get;
        set => SetProperty(ref field, value);
    }

    public int FocusScore
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string FocusReason
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsClassifying
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ShowCheckingMessage));
                OnPropertyChanged(nameof(ShowMarkOverrideButton));
            }
        }
    }

    public bool HasCurrentFocusResult
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ShowCheckingMessage));
                OnPropertyChanged(nameof(ShowMarkOverrideButton));
            }
        }
    }

    public string FocusScoreCategory =>
        FocusScore >= 6 ? "Focused"
        : FocusScore >= 4 ? "Unclear"
        : "Distracted";

    public string FocusStatusIcon =>
        (IsMonitoring && !HasCurrentFocusResult)
            ? "ms-appx:///Assets/icon-unclear.svg"
            : FocusScore switch
            {
                >= 6 => "ms-appx:///Assets/icon-focused.svg",
                >= 4 => "ms-appx:///Assets/icon-unclear.svg",
                _ => "ms-appx:///Assets/icon-distracted.svg",
            };

    public string FocusAccentBrushKey =>
        FocusScore switch
        {
            >= 6 => "FbAlignedAccentBrush",
            >= 4 => "FbNeutralAccentBrush",
            _ => "FbMisalignedAccentBrush",
        };

    public bool ShowCheckingMessage => IsMonitoring && !HasCurrentFocusResult;

    public bool ShowMarkOverrideButton => HasCurrentFocusResult && !IsClassifying && !IsNeutralApp;

    /// <summary>
    /// True when the current foreground app is considered neutral (not subject to focus classification).
    /// </summary>
    private bool IsNeutralApp =>
        FocusReason.Contains("neutral", StringComparison.OrdinalIgnoreCase);

    public string MarkOverrideButtonText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Mark as distracting";

    public FocusStatusViewModel(
        IFocusSessionOrchestrator sessionOrchestrator,
        IUIThreadDispatcher? uiDispatcher = null)
    {
        _sessionOrchestrator = sessionOrchestrator;
        _uiDispatcher = uiDispatcher;

        _sessionOrchestrator.StateChanged += OnOrchestratorStateChanged;
    }

    private void OnOrchestratorStateChanged(object? sender, FocusSessionStateChangedEventArgs e)
    {
        void UpdateState()
        {
            IsClassifying = e.IsClassifying;
            FocusScore = e.FocusScore;
            FocusReason = e.FocusReason;
            HasCurrentFocusResult = e.HasCurrentFocusResult;
            CurrentProcessName = e.CurrentProcessName;
            CurrentWindowTitle = e.CurrentWindowTitle;
            MarkOverrideButtonText = e.FocusScore >= 6 ? "Mark as distracting" : "Mark as focused";

            OnPropertyChanged(nameof(FocusScoreCategory));
            OnPropertyChanged(nameof(FocusStatusIcon));
            OnPropertyChanged(nameof(FocusAccentBrushKey));
        }

        if (_uiDispatcher != null)
        {
            _ = _uiDispatcher.RunOnUIThreadAsync(() =>
            {
                UpdateState();
                return Task.CompletedTask;
            });
        }
        else
        {
            UpdateState();
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task MarkFocusOverrideAsync()
    {
        int newScore = FocusScore >= 6 ? 2 : 9;
        string newReason =
            FocusScore >= 6 ? "Manually marked as Distracting" : "Manually marked as Focused";

        await _sessionOrchestrator.RecordManualOverrideAsync(newScore, newReason);
    }

    /// <summary>
    /// Resets all display state. Called by the parent when a session ends.
    /// </summary>
    public void Reset()
    {
        CurrentProcessName = string.Empty;
        CurrentWindowTitle = string.Empty;
        FocusScore = 0;
        FocusReason = string.Empty;
        HasCurrentFocusResult = false;
        IsMonitoring = false;

        OnPropertyChanged(nameof(FocusScoreCategory));
        OnPropertyChanged(nameof(FocusStatusIcon));
        OnPropertyChanged(nameof(FocusAccentBrushKey));
        OnPropertyChanged(nameof(ShowCheckingMessage));
        OnPropertyChanged(nameof(ShowMarkOverrideButton));
    }
}
