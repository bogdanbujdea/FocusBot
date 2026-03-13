namespace FocusBot.Core.Entities;

/// <summary>
/// Daily aggregate of focus analytics for a single local calendar day.
/// </summary>
public class DailyFocusAnalytics
{
    public int Id { get; set; }

    public DateOnly AnalyticsDateLocal { get; set; }

    public int TotalTrackedSeconds { get; set; }

    public int FocusedSeconds { get; set; }

    public int UnclearSeconds { get; set; }

    public int DistractedSeconds { get; set; }

    public int DistractionCount { get; set; }

    public int? AverageDistractionSeconds { get; set; }
}

