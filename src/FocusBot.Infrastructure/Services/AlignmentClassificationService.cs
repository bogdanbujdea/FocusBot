using CSharpFunctionalExtensions;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Classifies the alignment of the current foreground window with the active task via the WebAPI.
/// When the cached subscription plan is <see cref="ClientPlanType.CloudBYOK"/>, reads the API key,
/// provider, and model from settings and passes the key as a header to the backend.
/// All caching is handled server-side.
/// </summary>
public class AlignmentClassificationService(
    IFocusBotApiClient apiClient,
    ISettingsService settings,
    IPlanService planService,
    ILogger<AlignmentClassificationService> logger
) : IClassificationService
{
    public async Task<Result<AlignmentResult>> ClassifyAsync(
        string processName,
        string windowTitle,
        string sessionText,
        string? sessionContext,
        CancellationToken ct = default
    )
    {
        if (!apiClient.IsConfigured)
            return Result.Failure<AlignmentResult>("Not authenticated. Sign in to classify.");

        return await ClassifyViaApiAsync(processName, windowTitle, sessionText, sessionContext, ct);
    }

    private async Task<Result<AlignmentResult>> ClassifyViaApiAsync(
        string processName,
        string windowTitle,
        string sessionTitle,
        string? sessionContext,
        CancellationToken ct
    )
    {
        logger.LogInformation(
            "Desktop app requesting classification | Process: {ProcessName} | Window: {WindowTitle}",
            processName,
            windowTitle
        );

        var byokKey = await GetByokApiKeyAsync(ct);
        var providerId = await settings.GetProviderAsync();
        var modelId = await settings.GetModelAsync();

        var payload = new ClassifyPayload(
            sessionTitle,
            sessionContext,
            processName,
            windowTitle,
            providerId,
            modelId
        );

        var response = await apiClient.ClassifyAsync(payload, byokKey);

        if (response is null)
        {
            logger.LogWarning(
                "Desktop classification failed | Process: {ProcessName} | Window: {WindowTitle}",
                processName,
                windowTitle
            );
            return Result.Failure<AlignmentResult>(
                "Classification request failed. Check your connection."
            );
        }

        var classification =
            response.Score > 5 ? "Aligned"
            : response.Score < 5 ? "Distracting"
            : "Neutral";
        logger.LogInformation(
            "Desktop classification received: {Classification} (score={Score}) | Process: {ProcessName} | Window: {WindowTitle} | Cached: {Cached}",
            classification,
            response.Score,
            processName,
            windowTitle,
            response.Cached
        );

        var result = new AlignmentResult { Score = response.Score, Reason = response.Reason };

        return Result.Success(result);
    }

    private async Task<string?> GetByokApiKeyAsync(CancellationToken ct)
    {
        try
        {
            var plan = await planService.GetCurrentPlanAsync(ct);
            if (plan != ClientPlanType.CloudBYOK)
                return null;

            var key = await settings.GetApiKeyAsync();
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read BYOK API key from settings");
            return null;
        }
    }
}
