using System.Text.Json;
using FocusBot.Core.Configuration;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Configuration;
using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.Code;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Service that classifies window/task alignment using an LLM (provider and model from settings).
/// </summary>
public class LlmService(ISettingsService settingsService, ILogger<LlmService> logger) : ILlmService
{
    private const string SystemPrompt = """
        You are a focus alignment classifier for a Windows productivity app. The app tracks the user's currently active (foreground) window and asks you to determine if it aligns with their current task.

        You will receive:
        - Task: What the user is trying to accomplish
        - Context (optional): Additional hints provided by the user about relevant apps, websites, or keywords
        - Process name: The Windows executable (e.g., "msedge", "notepad", "code")
        - Window title: The title bar text of the active window

        RULES:
        1. User-provided context is authoritative. If context mentions specific websites, apps, locations, or keywords, look for ANY connection in the window title - including abbreviations, codes, partial matches, or domain-specific shorthand.
        2. Apply domain knowledge liberally. Window titles often use abbreviated forms, industry codes, or shorthand that relate to the task even when not an exact match.
        3. When in doubt and context was provided, favor a higher score - the user knows their task better than you do.
        4. For browsers (chrome, msedge, firefox, brave, opera), the window title shows the current page/tab. Infer meaning even if the website name isn't explicitly shown.

        Respond with valid JSON only: {"score": N, "reason": "brief explanation"}

        Score guidelines (1-10):
        - 9-10: Directly executing the task or on a resource explicitly mentioned in context
        - 7-8: Strongly supports the task (related tools, research, reference material)
        - 5-6: Possibly related but connection is unclear
        - 3-4: Unlikely to be related
        - 1-2: Clearly off-task (entertainment, social media unrelated to work)
        """;

    public async Task<AlignmentResult?> ClassifyAlignmentAsync(
        string taskDescription,
        string? taskContext,
        string processName,
        string windowTitle,
        CancellationToken ct = default
    )
    {
        var apiKey = await settingsService.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var providerId =
            await settingsService.GetProviderAsync()
            ?? FocusBot.Core.Configuration.LlmProviderConfig.DefaultProvider.ProviderId;
        var model =
            await settingsService.GetModelAsync()
            ?? FocusBot.Core.Configuration.LlmProviderConfig.DefaultModel(providerId).ModelId;

        try
        {
            var userMessage = string.IsNullOrWhiteSpace(taskContext)
                ? $"Task: {taskDescription}\n\nCurrent window: Application = {processName}, Title = {windowTitle}"
                : $"Task: {taskDescription}\n\nContext provided by the user: {taskContext}\n\nCurrent window: Application = {processName}, Title = {windowTitle}";

            var provider = Configuration.LlmProviderConfig.ToLlmProvider(providerId);
            var api = new TornadoApi(provider, apiKey);

            var response = await api
                .Chat.CreateConversation(model)
                .AppendSystemMessage(SystemPrompt)
                .AppendUserInput(userMessage)
                .GetResponse(ct);

            if (string.IsNullOrWhiteSpace(response))
                return null;

            return ParseResponse(response);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Alignment classification failed");
            return null;
        }
    }

    private static AlignmentResult? ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var score = root.TryGetProperty("score", out var scoreProp) ? scoreProp.GetInt32() : 0;
            var reason = root.TryGetProperty("reason", out var reasonProp)
                ? reasonProp.GetString() ?? string.Empty
                : string.Empty;
            score = Math.Clamp(score, 1, 10);
            return new AlignmentResult { Score = score, Reason = reason };
        }
        catch
        {
            return null;
        }
    }
}
