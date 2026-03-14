using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.DTOs;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

/// <summary>
/// ViewModel for the companion (follower) mode view, displayed when the browser extension leads a task.
/// </summary>
public partial class CompanionViewModel : ObservableObject
{
    private readonly IIntegrationService _integrationService;

    public CompanionViewModel(IIntegrationService integrationService)
    {
        _integrationService = integrationService;
        _integrationService.FocusStatusReceived += OnFocusStatusReceived;
        _integrationService.TaskEndedReceived += OnTaskEndedReceived;
    }

    public string TaskName
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string Classification
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string Reason
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public int FocusScorePercent
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string ContextType
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string ContextTitle
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string StatusBadgeText => Classification switch
    {
        "Focused" or "Aligned" => "Aligned",
        "Distracted" or "Distracting" => "Distracting",
        _ => "Waiting..."
    };

    public string StatusBadgeColor => Classification switch
    {
        "Focused" or "Aligned" => "FbAlignedAccentBrush",
        "Distracted" or "Distracting" => "FbMisalignedAccentBrush",
        _ => "FbNeutralAccentBrush"
    };

    public string FocusScoreDisplay => $"{FocusScorePercent}% Focused";

    public string ContextDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(ContextTitle))
                return string.Empty;

            var prefix = string.Equals(ContextType, "browser", StringComparison.OrdinalIgnoreCase)
                ? "Browser"
                : "Desktop";
            return $"{prefix}: {ContextTitle}";
        }
    }

    /// <summary>
    /// Raised when the extension ends the task and the app should return to standalone/kanban mode.
    /// </summary>
    public event EventHandler? ReturnToStandalone;

    public void ApplyTaskStarted(TaskStartedPayload payload)
    {
        TaskName = payload.TaskText;
        Classification = string.Empty;
        Reason = string.Empty;
        FocusScorePercent = 0;
        ContextType = string.Empty;
        ContextTitle = string.Empty;
        NotifyAll();
    }

    private void OnFocusStatusReceived(object? sender, FocusStatusPayload payload)
    {
        Classification = payload.Classification;
        Reason = payload.Reason;
        FocusScorePercent = payload.FocusScorePercent;
        ContextType = payload.ContextType;
        ContextTitle = payload.ContextTitle;
        NotifyAll();
    }

    private void OnTaskEndedReceived(object? sender, EventArgs e)
    {
        TaskName = string.Empty;
        Classification = string.Empty;
        Reason = string.Empty;
        FocusScorePercent = 0;
        ContextType = string.Empty;
        ContextTitle = string.Empty;
        NotifyAll();
        ReturnToStandalone?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Disconnect()
    {
        ReturnToStandalone?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(StatusBadgeColor));
        OnPropertyChanged(nameof(FocusScoreDisplay));
        OnPropertyChanged(nameof(ContextDisplay));
    }
}
