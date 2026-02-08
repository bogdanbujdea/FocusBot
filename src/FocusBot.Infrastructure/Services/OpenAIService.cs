using System.Text.Json;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Service that classifies window/task alignment using the OpenAI API (no caching).
/// </summary>
public class OpenAIService(ISettingsService settingsService, ILogger<OpenAIService> logger)
    : IOpenAIService
{
    private const string ModelName = "gpt-4o-mini";

    private const string SystemPrompt = """
        You are a focus alignment classifier. Given a user's task and their current window/application, determine how aligned the window is with the task.

        Respond with valid JSON only: {"score": N, "reason": "brief explanation"}

        Score guidelines (1-10):
        - 1-4: Window does not support the stated task
        - 5: Ambiguous or unclear relationship
        - 6-10: Window supports or directly relates to the task

        For BROWSERS (chrome, msedge, firefox, opera, brave): Use ONLY the window title to determine alignment since it shows the current page/tab content.
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

        try
        {
            var userMessage = string.IsNullOrWhiteSpace(taskContext)
                ? $"Task: {taskDescription}\n\nCurrent window: Application = {processName}, Title = {windowTitle}"
                : $"Task: {taskDescription}\n\nContext provided by the user: {taskContext}\n\nCurrent window: Application = {processName}, Title = {windowTitle}";

            var client = new ChatClient(ModelName, apiKey);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(userMessage),
            };

            var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
            var text = completion.Value.Content?[0].Text;
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return ParseResponse(text);
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
