using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

public partial class TaskDetailViewModel(
    ITaskRepository repo,
    INavigationService navigationService
) : ObservableObject
{
    private string? _taskId;

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    private string _context = string.Empty;
    public string Context
    {
        get => _context;
        private set => SetProperty(ref _context, value);
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private string _totalTime = "00:00:00";
    public string TotalTime
    {
        get => _totalTime;
        private set => SetProperty(ref _totalTime, value);
    }

    private string _focusScore = string.Empty;
    public string FocusScore
    {
        get => _focusScore;
        private set => SetProperty(ref _focusScore, value);
    }

    public bool HasContext => !string.IsNullOrEmpty(Context);
    public bool HasFocusScore => !string.IsNullOrEmpty(FocusScore);

    public ObservableCollection<WindowActivityItem> WindowActivity { get; } = new();

    public async Task InitializeAsync(string taskId)
    {
        _taskId = taskId;
        var task = await repo.GetByIdAsync(taskId);
        if (task == null)
        {
            navigationService.NavigateToBoard();
            return;
        }

        Description = task.Description;
        Context = task.Context ?? string.Empty;
        Status = task.Status.ToString();
        TotalTime = FormatElapsed(task.TotalElapsedSeconds);
        FocusScore = task.FocusScorePercent.HasValue ? $"{task.FocusScorePercent}%" : string.Empty;

        OnPropertyChanged(nameof(HasContext));
        OnPropertyChanged(nameof(HasFocusScore));

        await LoadWindowActivityAsync(taskId);
    }

    private async Task LoadWindowActivityAsync(string taskId)
    {
        WindowActivity.Clear();
        var segments = await repo.GetFocusSegmentsForTaskAsync(taskId);
        var sortedSegments = segments
            .OrderByDescending(s => s.DurationSeconds)
            .ToList();

        foreach (var segment in sortedSegments)
        {
            WindowActivity.Add(new WindowActivityItem
            {
                ProcessName = segment.ProcessName ?? "Unknown",
                WindowTitle = segment.WindowTitle ?? "Unknown",
                Duration = FormatElapsed(segment.DurationSeconds),
                DurationSeconds = segment.DurationSeconds,
                AlignmentScore = segment.AlignmentScore
            });
        }
    }

    private static string FormatElapsed(long totalSeconds)
    {
        var hours = (int)(totalSeconds / 3600);
        var minutes = (int)((totalSeconds % 3600) / 60);
        var seconds = (int)(totalSeconds % 60);
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    [RelayCommand]
    private void Back() => navigationService.NavigateToBoard();
}

public class WindowActivityItem
{
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public int AlignmentScore { get; set; }
}
