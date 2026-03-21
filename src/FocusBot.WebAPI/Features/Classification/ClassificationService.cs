using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using LlmTornado;
using LlmTornado.Code;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Features.Classification;

/// <summary>
/// Classifies window/task alignment via LLM with a 24-hour result cache.
/// Supports BYOK (bring-your-own-key) and managed API key modes.
/// </summary>
public class ClassificationService(
    ApiDbContext db,
    IConfiguration configuration,
    ILogger<ClassificationService> logger)
{
    private const string DefaultProviderId = "OpenAi";
    private const string DefaultModelId = "gpt-4o-mini";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

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
        - 7-8: Strongly supports the task (related tools, reference material, research directly relevant to the task)
        - 5-6: Possibly related but connection is unclear
        - 3-4: Unlikely to be related to the task
        - 1-2: Clearly off-task (entertainment, social media, or work unrelated to the task)
        """;

    /// <summary>
    /// Classifies the alignment of a window/process with the user's task.
    /// Returns a cached result when available, otherwise calls the LLM.
    /// </summary>
    public async Task<ClassifyResponse> ClassifyAsync(
        Guid userId,
        ClassifyRequest request,
        string? byokApiKey,
        CancellationToken ct = default)
    {
        var contextHash = ComputeContextHash(request.ProcessName, request.WindowTitle, request.Url, request.PageTitle);
        var taskContentHash = ComputeTaskContentHash(request.SessionTitle, request.SessionContext);

        var cached = await TryGetCachedResultAsync(userId, contextHash, taskContentHash, ct);
        if (cached is not null)
            return cached;

        var (apiKey, providerId, modelId) = ResolveCredentials(request, byokApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("No API key available. Provide an X-Api-Key header or configure ManagedOpenAiKey.");

        var result = await CallLlmAsync(apiKey, providerId, modelId, request, ct);
        await CacheResultAsync(userId, contextHash, taskContentHash, result.Score, result.Reason, ct);

        return result;
    }

    public static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    public static string ComputeContextHash(
        string? processName, string? windowTitle, string? url, string? pageTitle)
    {
        return ComputeHash($"{processName}|{windowTitle}|{url}|{pageTitle}");
    }

    public static string ComputeTaskContentHash(string sessionTitle, string? sessionContext)
    {
        return ComputeHash($"{sessionTitle}|{sessionContext ?? string.Empty}");
    }

    private async Task<ClassifyResponse?> TryGetCachedResultAsync(
        Guid userId, string contextHash, string taskContentHash, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var entry = await db.Set<ClassificationCache>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.UserId == userId &&
                c.ContextHash == contextHash &&
                c.TaskContentHash == taskContentHash &&
                c.ExpiresAtUtc > now, ct);

        if (entry is null)
            return null;

        return new ClassifyResponse(entry.Score, entry.Reason, Cached: true);
    }

    private (string ApiKey, string ProviderId, string ModelId) ResolveCredentials(
        ClassifyRequest request, string? byokApiKey)
    {
        if (!string.IsNullOrWhiteSpace(byokApiKey))
        {
            var providerId = request.ProviderId ?? DefaultProviderId;
            var modelId = request.ModelId ?? DefaultModelId;
            return (byokApiKey, providerId, modelId);
        }

        var managedKey = configuration["ManagedOpenAiKey"] ?? string.Empty;
        return (managedKey, DefaultProviderId, DefaultModelId);
    }

    /// <summary>
    /// Validates a BYOK API key by making a minimal test request to the chosen provider.
    /// Returns a structured result without throwing.
    /// </summary>
    public async Task<ValidateKeyResponse> ValidateKeyAsync(
        string providerId, string modelId, string apiKey, CancellationToken ct = default)
    {
        try
        {
            var provider = MapProvider(providerId);
            var api = new TornadoApi(provider, apiKey);

            var response = await api
                .Chat.CreateConversation(modelId)
                .AppendUserInput("Ping")
                .GetResponse(ct);

            return new ValidateKeyResponse(Valid: true, Error: null);
        }
        catch (Exception ex) when (IsInvalidKeyException(ex))
        {
            return new ValidateKeyResponse(Valid: false, Error: "invalid_key");
        }
        catch (Exception ex) when (IsRateLimitException(ex))
        {
            return new ValidateKeyResponse(Valid: false, Error: "rate_limited");
        }
        catch (Exception)
        {
            return new ValidateKeyResponse(Valid: false, Error: "provider_unavailable");
        }
    }

    protected virtual async Task<ClassifyResponse> CallLlmAsync(
        string apiKey,
        string providerId,
        string modelId,
        ClassifyRequest request,
        CancellationToken ct)
    {
        var userMessage = BuildUserMessage(request);
        var provider = MapProvider(providerId);
        var api = new TornadoApi(provider, apiKey);

        try
        {
            var response = await api
                .Chat.CreateConversation(modelId)
                .AppendSystemMessage(SystemPrompt)
                .AppendUserInput(userMessage)
                .GetResponse(ct);

            if (string.IsNullOrWhiteSpace(response))
                throw new InvalidOperationException("LLM returned an empty response.");

            return ParseLlmResponse(response);
        }
        catch (Exception ex) when (IsInvalidKeyException(ex))
        {
            throw new ClassificationProviderException(ClassificationErrorCode.InvalidKey, "The API key is invalid or has been revoked.", ex);
        }
        catch (Exception ex) when (IsRateLimitException(ex))
        {
            throw new ClassificationProviderException(ClassificationErrorCode.RateLimited, "The LLM provider rate limit has been reached. Please try again later.", ex);
        }
        catch (Exception ex) when (ex is not ClassificationProviderException && ex is not OperationCanceledException)
        {
            throw new ClassificationProviderException(ClassificationErrorCode.ProviderUnavailable, $"The LLM provider is currently unavailable: {ex.Message}", ex);
        }
    }

    private static bool IsInvalidKeyException(Exception ex) =>
        ex.Message.Contains("401", StringComparison.Ordinal) ||
        ex.Message.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase);

    private static bool IsRateLimitException(Exception ex) =>
        ex.Message.Contains("429", StringComparison.Ordinal) ||
        ex.Message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);

    private static string BuildUserMessage(ClassifyRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("Task: ").AppendLine(request.SessionTitle);

        if (!string.IsNullOrWhiteSpace(request.SessionContext))
            sb.Append("\nContext provided by the user: ").AppendLine(request.SessionContext);

        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(request.ProcessName) || !string.IsNullOrWhiteSpace(request.WindowTitle))
            sb.Append("Current window: Application = ").Append(request.ProcessName).Append(", Title = ").AppendLine(request.WindowTitle);

        if (!string.IsNullOrWhiteSpace(request.Url))
            sb.Append("URL: ").AppendLine(request.Url);

        if (!string.IsNullOrWhiteSpace(request.PageTitle))
            sb.Append("Page title: ").AppendLine(request.PageTitle);

        return sb.ToString();
    }

    public static LLmProviders MapProvider(string providerId)
    {
        return providerId switch
        {
            "OpenAi" => LLmProviders.OpenAi,
            "Anthropic" => LLmProviders.Anthropic,
            "Google" => LLmProviders.Google,
            _ => LLmProviders.OpenAi,
        };
    }

    public static ClassifyResponse ParseLlmResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var score = root.TryGetProperty("score", out var scoreProp) ? scoreProp.GetInt32() : 5;
            var reason = root.TryGetProperty("reason", out var reasonProp)
                ? reasonProp.GetString() ?? string.Empty
                : string.Empty;
            score = Math.Clamp(score, 1, 10);
            return new ClassifyResponse(score, reason, Cached: false);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("LLM returned invalid JSON.");
        }
    }

    private async Task CacheResultAsync(
        Guid userId,
        string contextHash,
        string taskContentHash,
        int score,
        string reason,
        CancellationToken ct)
    {
        try
        {
            var entry = new ClassificationCache
            {
                UserId = userId,
                ContextHash = contextHash,
                TaskContentHash = taskContentHash,
                Score = score,
                Reason = reason,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.Add(CacheDuration)
            };

            db.Set<ClassificationCache>().Add(entry);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Failed to cache classification result; continuing without cache");
        }
    }
}
