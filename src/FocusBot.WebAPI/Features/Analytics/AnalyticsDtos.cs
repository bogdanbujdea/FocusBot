namespace FocusBot.WebAPI.Features.Analytics;

/// <summary>Response for GET /analytics/summary — aggregated metrics for a date range.</summary>
public sealed record AnalyticsSummaryResponse(
    DateRange Period,
    int TotalSessions,
    long TotalFocusedSeconds,
    long TotalDistractedSeconds,
    int AverageFocusScorePercent,
    int TotalDistractionCount,
    int TotalContextSwitchCount,
    long AverageSessionDurationSeconds,
    long LongestSessionSeconds,
    int DevicesActive
);

/// <summary>A date range with inclusive start and exclusive end.</summary>
public sealed record DateRange(DateTime From, DateTime To);

/// <summary>Response for GET /analytics/trends — time-series data points.</summary>
public sealed record AnalyticsTrendsResponse(string Granularity, IReadOnlyList<TrendDataPoint> DataPoints);

/// <summary>A single data point in a time-series trend.</summary>
public sealed record TrendDataPoint(
    string Date,
    int Sessions,
    long FocusedSeconds,
    long DistractedSeconds,
    int FocusScorePercent,
    int DistractionCount
);

/// <summary>Response for GET /analytics/devices — per-device breakdown.</summary>
public sealed record AnalyticsDevicesResponse(IReadOnlyList<DeviceAnalytics> Devices);

/// <summary>Analytics breakdown for a single device.</summary>
public sealed record DeviceAnalytics(
    Guid DeviceId,
    string DeviceType,
    string Name,
    int Sessions,
    long FocusedSeconds,
    long DistractedSeconds,
    int FocusScorePercent
);
