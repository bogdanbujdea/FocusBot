namespace FocusBot.Core.DTOs;

public class SessionDistractionSummary
{
    public int TotalDistractionCount { get; set; }

    public IReadOnlyList<AppDistractionSummary> TopApps { get; set; } =
        Array.Empty<AppDistractionSummary>();
}
