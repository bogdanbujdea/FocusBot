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
