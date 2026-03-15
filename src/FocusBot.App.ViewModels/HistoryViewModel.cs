using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;

namespace FocusBot.App.ViewModels;

/// <summary>
/// Date range filter for history view.
/// </summary>
public enum DateRange
{
    Today,
    Week,
    Month,
    All
}

/// <summary>
/// Group of completed tasks for a single day with aggregated stats.
/// </summary>
public class DailyStatGroup
{
    public DateOnly Date { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public int TaskCount { get; init; }
    public int AverageFocusScore { get; init; }
    public long FocusedSeconds { get; init; }
    public long DistractedSeconds { get; init; }
    public string FocusedTimeText { get; init; } = string.Empty;
    /// <summary>Percentage of tracked time that was aligned (0-100). Used for chart bar segments.</summary>
    public double AlignedPercent { get; init; }
    /// <summary>Percentage of tracked time that was distracting (0-100).</summary>
    public double DistractedPercent { get; init; }
    public ObservableCollection<UserTask> Tasks { get; } = new();
}

public partial class HistoryViewModel(
    ITaskRepository repo,
    INavigationService navigationService
) : ObservableObject
{
    [ObservableProperty]
    private DateRange _selectedRange = DateRange.Week;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalTasks;

    [ObservableProperty]
    private long _totalFocusedSeconds;

    [ObservableProperty]
    private long _totalDistractedSeconds;

    [ObservableProperty]
    private int _totalDistractions;

    [ObservableProperty]
    private int _averageFocusScore;

    public ObservableCollection<DailyStatGroup> DailyStats { get; } = new();

    public string TotalFocusedTimeText => FormatTimeShort(TotalFocusedSeconds);
    public string TotalDistractedTimeText => FormatTimeShort(TotalDistractedSeconds);
    public string TotalTrackedTimeText => FormatTimeShort(TotalFocusedSeconds + TotalDistractedSeconds);
    public string FocusedPercentText => ComputeFocusedPercent(TotalFocusedSeconds, TotalDistractedSeconds);
    public bool HasData => TotalTasks > 0;
    public bool ShowEmptyState => !IsLoading && !HasData;

    public int FocusPercentage => ComputeFocusPercentage(TotalFocusedSeconds, TotalDistractedSeconds);
    public double GaugeStrokeDashOffset => 263.89 * (100 - Math.Clamp(FocusPercentage, 0, 100)) / 100.0;
    public string AvgDistractionDurationText => TotalDistractions > 0 && TotalDistractedSeconds >= 0
        ? FormatTimeShort(TotalDistractedSeconds / TotalDistractions)
        : string.Empty;

    public string AvgDistractionDurationSublabel => TotalDistractions > 0 && TotalDistractedSeconds >= 0
        ? $"Avg {FormatTimeShort(TotalDistractedSeconds / TotalDistractions)} each"
        : "None recorded";

    public string TotalTrackedTimeSublabel => $"{TotalTrackedTimeText} tracked";
    public string AvgSessionLengthText => TotalTasks > 0 ? FormatTimeShort((TotalFocusedSeconds + TotalDistractedSeconds) / TotalTasks) : string.Empty;
    public string BestFocusDayDisplay => GetBestFocusDayDisplay();
    public bool ShowBestFocusDay => !string.IsNullOrEmpty(BestFocusDayDisplay);
    public bool ShowDailyChart => SelectedRange != DateRange.Today && DailyStats.Count > 1;

    private List<UserTask> _allDoneTasks = new();
    private static readonly TimeZoneInfo LocalTz = TimeZoneInfo.Local;

    partial void OnSelectedRangeChanged(DateRange value)
    {
        ApplyFilter();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        OnPropertyChanged(nameof(ShowEmptyState));
        try
        {
            var tasks = await repo.GetDoneTasksAsync();
            _allDoneTasks = tasks.ToList();
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await InitializeAsync();
    }

    [RelayCommand]
    private void ChangeRange(object? parameter)
    {
        if (parameter is DateRange range)
        {
            SelectedRange = range;
            return;
        }
        if (parameter is string s && Enum.TryParse<DateRange>(s, ignoreCase: true, out var parsed))
            SelectedRange = parsed;
    }

    [RelayCommand]
    private void Back()
    {
        navigationService.NavigateToBoard();
    }

    [RelayCommand]
    private void ViewTaskDetail(string taskId)
    {
        if (!string.IsNullOrEmpty(taskId))
            navigationService.NavigateToTaskDetail(taskId);
    }

    private void ApplyFilter()
    {
        var filtered = FilterByDateRange(_allDoneTasks, SelectedRange);
        GroupByDay(filtered);
        UpdateSummaryAggregates(filtered);
        NotifySummaryProperties();
    }

    private static List<UserTask> FilterByDateRange(List<UserTask> tasks, DateRange range)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);

        return range switch
        {
            DateRange.Today => tasks.Where(t => DateOnly.FromDateTime(ToLocal(t.CreatedAt)) == today).ToList(),
            DateRange.Week => tasks.Where(t =>
            {
                var d = DateOnly.FromDateTime(ToLocal(t.CreatedAt));
                var diff = today.DayNumber - d.DayNumber;
                return diff >= 0 && diff < 7;
            }).ToList(),
            DateRange.Month => tasks.Where(t =>
            {
                var d = DateOnly.FromDateTime(ToLocal(t.CreatedAt));
                var diff = today.DayNumber - d.DayNumber;
                return diff >= 0 && diff < 30;
            }).ToList(),
            DateRange.All => tasks.ToList(),
            _ => tasks.ToList()
        };
    }

    private void GroupByDay(List<UserTask> filtered)
    {
        DailyStats.Clear();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);

        var byDay = filtered
            .GroupBy(t => DateOnly.FromDateTime(ToLocal(t.CreatedAt)))
            .OrderByDescending(g => g.Key);

        foreach (var group in byDay)
        {
            var date = group.Key;
            var tasks = group.OrderByDescending(t => t.CreatedAt).ToList();
            var focusedSum = tasks.Sum(t => t.FocusedSeconds);
            var scoreValues = tasks.Where(t => t.FocusScorePercent.HasValue).Select(t => t.FocusScorePercent!.Value).ToList();
            var avgScore = scoreValues.Count > 0 ? (int)Math.Round(scoreValues.Average()) : 0;

            var dateDisplay = date == today ? "Today"
                : date == yesterday ? "Yesterday"
                : date.ToString("MMM d");

            var distractedSum = tasks.Sum(t => t.DistractedSeconds);
            var totalTracked = focusedSum + distractedSum;
            var alignedPercent = totalTracked > 0 ? 100.0 * focusedSum / totalTracked : 50.0;
            var distractedPercent = totalTracked > 0 ? 100.0 * distractedSum / totalTracked : 50.0;

            DailyStats.Add(new DailyStatGroup
            {
                Date = date,
                DateDisplay = dateDisplay,
                TaskCount = tasks.Count,
                AverageFocusScore = avgScore,
                FocusedSeconds = focusedSum,
                DistractedSeconds = distractedSum,
                FocusedTimeText = FormatTimeShort(focusedSum),
                AlignedPercent = alignedPercent,
                DistractedPercent = distractedPercent
            });

            foreach (var t in tasks)
                DailyStats[DailyStats.Count - 1].Tasks.Add(t);
        }
    }

    private void UpdateSummaryAggregates(List<UserTask> filtered)
    {
        TotalTasks = filtered.Count;
        TotalFocusedSeconds = filtered.Sum(t => t.FocusedSeconds);
        TotalDistractedSeconds = filtered.Sum(t => t.DistractedSeconds);
        TotalDistractions = filtered.Sum(t => t.DistractionCount);

        var scores = filtered.Where(t => t.FocusScorePercent.HasValue).Select(t => t.FocusScorePercent!.Value).ToList();
        AverageFocusScore = scores.Count > 0 ? (int)Math.Round(scores.Average()) : 0;
    }

    private void NotifySummaryProperties()
    {
        OnPropertyChanged(nameof(TotalFocusedTimeText));
        OnPropertyChanged(nameof(TotalDistractedTimeText));
        OnPropertyChanged(nameof(TotalTrackedTimeText));
        OnPropertyChanged(nameof(FocusedPercentText));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(FocusPercentage));
        OnPropertyChanged(nameof(GaugeStrokeDashOffset));
        OnPropertyChanged(nameof(AvgDistractionDurationText));
        OnPropertyChanged(nameof(AvgDistractionDurationSublabel));
        OnPropertyChanged(nameof(TotalTrackedTimeSublabel));
        OnPropertyChanged(nameof(AvgSessionLengthText));
        OnPropertyChanged(nameof(BestFocusDayDisplay));
        OnPropertyChanged(nameof(ShowBestFocusDay));
        OnPropertyChanged(nameof(ShowDailyChart));
    }

    private static int ComputeFocusPercentage(long focused, long distracted)
    {
        var total = focused + distracted;
        if (total == 0) return 0;
        return (int)Math.Round(100.0 * focused / total);
    }

    private string GetBestFocusDayDisplay()
    {
        if (SelectedRange == DateRange.Today || DailyStats.Count == 0)
            return string.Empty;
        var best = DailyStats.MaxBy(d => d.AverageFocusScore);
        if (best == null || best.TaskCount == 0)
            return string.Empty;
        var dayLabel = best.Date.ToString("dddd, MMM d");
        return $"{dayLabel} ({best.AverageFocusScore}%)";
    }

    private static DateTime ToLocal(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utc, LocalTz);
    }

    private static string FormatTimeShort(long totalSeconds)
    {
        if (totalSeconds < 60)
            return $"{totalSeconds}s";
        var hours = (int)(totalSeconds / 3600);
        var minutes = (int)((totalSeconds % 3600) / 60);
        if (hours > 0)
            return $"{hours}h {minutes}m";
        return $"{minutes}m";
    }

    private static string ComputeFocusedPercent(long focused, long distracted)
    {
        var total = focused + distracted;
        if (total == 0)
            return "—";
        var pct = (int)Math.Round(100.0 * focused / total);
        return $"{pct}%";
    }
}
