namespace FocusBot.Core.DTOs;

public class DailyFocusSummary
{
    public DateOnly AnalyticsDateLocal { get; set; }

    public int FocusScoreBucket { get; set; }

    public TimeSpan FocusedTime { get; set; }

    public TimeSpan DistractedTime { get; set; }

    public int DistractionCount { get; set; }

    public TimeSpan? AverageDistractionDuration { get; set; }

    public string? MostPopularDistractionApp { get; set; }

    public TimeSpan? LongestFocusedSession { get; set; }
}

