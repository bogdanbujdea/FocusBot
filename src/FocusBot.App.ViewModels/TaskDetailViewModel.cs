using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Helpers;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

public partial class TaskDetailViewModel(ISessionRepository repo, INavigationService navigationService)
    : ObservableObject
{
    public string Description
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string Context
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string Status
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string TotalTime
    {
        get;
        private set => SetProperty(ref field, value);
    } = "00:00:00";

    public string FocusScore
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public bool HasContext => !string.IsNullOrEmpty(Context);
    public bool HasFocusScore => !string.IsNullOrEmpty(FocusScore);

    public ObservableCollection<WindowActivityItem> WindowActivity { get; } = new();

    public async Task InitializeAsync(string taskId)
    {
        var task = await repo.GetByIdAsync(taskId);
        if (task == null)
        {
            navigationService.NavigateToBoard();
            return;
        }

        Description = task.Description;
        Context = task.Context ?? string.Empty;
        Status = task.IsCompleted ? "Completed" : "Active";
        TotalTime = TimeFormatHelper.FormatElapsed(task.TotalElapsedSeconds);
        FocusScore = task.FocusScorePercent.HasValue ? $"{task.FocusScorePercent}%" : string.Empty;

        OnPropertyChanged(nameof(HasContext));
        OnPropertyChanged(nameof(HasFocusScore));

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void Back() => navigationService.NavigateToBoard();
}

public class WindowActivityItem
{
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public int AlignmentScore { get; set; }
}
