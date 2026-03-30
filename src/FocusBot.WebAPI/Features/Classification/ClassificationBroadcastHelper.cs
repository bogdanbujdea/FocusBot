namespace FocusBot.WebAPI.Features.Classification;

/// <summary>
/// Derives hub broadcast fields from the coalescing winner request.
/// </summary>
internal static class ClassificationBroadcastHelper
{
    public static (string Source, string ActivityName) Describe(ClassifyRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Url))
            return ("extension", request.Url.Trim());

        var activity =
            !string.IsNullOrWhiteSpace(request.ProcessName)
                ? request.ProcessName.Trim()
                : (request.WindowTitle ?? string.Empty).Trim();

        return ("desktop", activity);
    }
}
