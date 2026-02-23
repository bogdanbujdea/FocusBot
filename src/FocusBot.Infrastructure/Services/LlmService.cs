using System.Text.Json;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using LlmTornado;
using Microsoft.Extensions.Logging;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Service that classifies window/task alignment using an LLM (provider and model from settings).
/// </summary>
public class LlmService(
    ISettingsService settingsService,
    ISubscriptionService subscriptionService,
    IManagedKeyProvider managedKeyProvider,
    ILogger<LlmService> logger
) : ILlmService
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

    public async Task<ClassifyAlignmentResponse> ClassifyAlignmentAsync(
        string taskDescription,
        string? taskContext,
        string processName,
        string windowTitle,
        CancellationToken ct = default
    )
    {
        var mode = await settingsService.GetApiKeyModeAsync();
        string apiKey;
        string providerId;
        string model;

        if (mode == ApiKeyMode.Managed)
        {
            var isSubscribed = await subscriptionService.IsSubscribedAsync();
            if (!isSubscribed)
            {
                return new ClassifyAlignmentResponse(
                    null,
                    "Please subscribe to use FocusBot Pro, or switch to using your own API key."
                );
            }

            var managedKey = await managedKeyProvider.GetApiKeyAsync();
            if (string.IsNullOrWhiteSpace(managedKey))
            {
                logger.LogError("Managed key provider returned null or empty key");
                return new ClassifyAlignmentResponse(
                    null,
                    "Unable to access AI service. Please try again or use your own API key."
                );
            }

            apiKey = managedKey;
            providerId = managedKeyProvider.ProviderId;
            model = managedKeyProvider.ModelId;
        }
        else
        {
            apiKey = await settingsService.GetApiKeyAsync() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
                return new ClassifyAlignmentResponse(null, null);

            providerId =
                await settingsService.GetProviderAsync()
                ?? FocusBot.Core.Configuration.LlmProviderConfig.DefaultProvider.ProviderId;
            model =
                await settingsService.GetModelAsync()
                ?? FocusBot.Core.Configuration.LlmProviderConfig.DefaultModel(providerId).ModelId;
        }

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
                return new ClassifyAlignmentResponse(null, null);

            var result = ParseResponse(response);
            return result != null
                ? new ClassifyAlignmentResponse(result, null)
                : new ClassifyAlignmentResponse(null, "Invalid response from AI.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Alignment classification failed");
            var userMessage = ToUserFriendlyErrorMessage(ex.Message);
            return new ClassifyAlignmentResponse(null, userMessage);
        }
    }

    public async Task<ClassifyAlignmentResponse> ValidateCredentialsAsync(
        string apiKey,
        string providerId,
        string modelId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new ClassifyAlignmentResponse(null, "Please enter an API key.");

        try
        {
            var provider = Configuration.LlmProviderConfig.ToLlmProvider(providerId);
            var api = new TornadoApi(provider, apiKey);
            var userMessage = "Ping";

            var response = await api
                .Chat.CreateConversation(modelId)
                .AppendUserInput(userMessage)
                .GetResponse(ct);

            if (string.IsNullOrWhiteSpace(response))
                return new ClassifyAlignmentResponse(null, "No response from API.");
            return new ClassifyAlignmentResponse(new AlignmentResult(), null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Credentials validation failed");
            var userMessage = ToUserFriendlyErrorMessage(ex.Message);
            return new ClassifyAlignmentResponse(null, userMessage);
        }
    }

    private static string ToUserFriendlyErrorMessage(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            return rawMessage ?? string.Empty;
        if (rawMessage.Contains("429", StringComparison.Ordinal))
            return "You've exceeded your API quota. Check your plan and billing in your provider's dashboard.";
        if (LooksLikeApiKeyOrRequestError(rawMessage))
            return "The API key may be incorrect or you may not have sufficient funds. Check your key and billing in your provider's dashboard.";
        return rawMessage;
    }

    private static bool LooksLikeApiKeyOrRequestError(string rawMessage)
    {
        if (rawMessage.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase))
            return true;
        if (rawMessage.Contains("Incorrect API key", StringComparison.OrdinalIgnoreCase))
            return true;
        if (rawMessage.Contains("invalid_request_error", StringComparison.OrdinalIgnoreCase))
            return true;
        if (
            rawMessage.IndexOf('{') >= 0
            && rawMessage.Contains("\"error\"", StringComparison.Ordinal)
        )
            return true;
        return false;
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
