using CommunityToolkit.Mvvm.ComponentModel;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for the current foreground window status bar shown during an active focus session.
/// Displays process name, window title, focus score, and classification status.
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
    }
}
