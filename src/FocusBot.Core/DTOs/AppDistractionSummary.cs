namespace FocusBot.Core.DTOs;

public class AppDistractionSummary
{
    public string AppName { get; set; } = string.Empty;

    public int DistractionCount { get; set; }

    public int DistractedDurationSeconds { get; set; }
}

