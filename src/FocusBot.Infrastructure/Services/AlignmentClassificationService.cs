using CSharpFunctionalExtensions;
using FocusBot.Core.Entities;
using FocusBot.Core.Helpers;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Classifies the alignment of the current foreground window with the active task.
/// Checks the local SQLite cache first; calls the backend API on a cache miss.
/// For BYOK users, reads the API key, provider, and model from settings and
/// passes the key as a header to the backend.
/// </summary>
public class AlignmentClassificationService(
    IAlignmentCacheRepository cache,
    IFocusBotApiClient apiClient,
    ISettingsService settings,
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

        var contextHash = HashHelper.ComputeWindowContextHash(processName, windowTitle);
        var taskContentHash = HashHelper.ComputeSessionContentHash(sessionText, sessionContext);

        var cached = await TryGetCachedAsync(contextHash, taskContentHash);
        if (cached is not null)
            return Result.Success(cached);

        return await ClassifyViaApiAsync(
            processName,
            windowTitle,
            sessionText,
            sessionContext,
            contextHash,
            taskContentHash,
            ct
        );
    }

    private async Task<AlignmentResult?> TryGetCachedAsync(
        string contextHash,
        string taskContentHash
    )
    {
        try
        {
            var entry = await cache.GetAsync(contextHash, taskContentHash);
            if (entry is null)
                return null;

            logger.LogDebug("Cache hit for context {ContextHash}", contextHash[..8]);
            return new AlignmentResult { Score = entry.Score, Reason = entry.Reason };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed; proceeding without cache");
            return null;
        }
    }

    private async Task<Result<AlignmentResult>> ClassifyViaApiAsync(
        string processName,
        string windowTitle,
        string taskText,
        string? taskHints,
        string contextHash,
        string taskContentHash,
        CancellationToken ct
    )
    {
        var byokKey = await GetByokApiKeyAsync();
        var providerId = await settings.GetProviderAsync();
        var modelId = await settings.GetModelAsync();

        var payload = new ClassifyPayload(
            taskText,
            taskHints,
            processName,
            windowTitle,
            providerId,
            modelId
        );
        var response = await apiClient.ClassifyAsync(payload, byokKey);

        if (response is null)
            return Result.Failure<AlignmentResult>(
                "Classification request failed. Check your connection."
            );

        var result = new AlignmentResult { Score = response.Score, Reason = response.Reason };

        if (!response.Cached)
            _ = CacheResultAsync(contextHash, taskContentHash, result);

        return Result.Success(result);
    }

    private async Task<string?> GetByokApiKeyAsync()
    {
        try
        {
            var mode = await settings.GetApiKeyModeAsync();
            if (mode == ApiKeyMode.Own)
                return await settings.GetApiKeyAsync();
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read BYOK API key from settings");
            return null;
        }
    }

    private async Task CacheResultAsync(
        string contextHash,
        string taskContentHash,
        AlignmentResult result
    )
    {
        try
        {
            var entry = new AlignmentCacheEntry
            {
                ContextHash = contextHash,
                TaskContentHash = taskContentHash,
                Score = result.Score,
                Reason = result.Reason,
                CreatedAt = DateTime.UtcNow,
            };
            await cache.SaveAsync(entry);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache classification result");
        }
    }
}
