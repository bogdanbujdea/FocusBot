namespace FocusBot.Core.Configuration;

/// <summary>
/// Curated list of LLM providers and models for the settings UI. Add new providers/models here.
/// </summary>
public static class LlmProviderConfig
{
    public static readonly List<ProviderInfo> Providers =
    [
        new("OpenAI", "OpenAi", "https://platform.openai.com/api-keys"),
        new("Anthropic", "Anthropic", "https://console.anthropic.com/settings/keys"),
        new("Google", "Google", "https://aistudio.google.com/apikey"),
    ];

    public static readonly Dictionary<string, List<ModelInfo>> Models = new()
    {
        ["OpenAi"] =
        [
            new("GPT-4o Mini", "gpt-4o-mini"),
            new("GPT-4.1 Mini", "gpt-4.1-mini"),
            new("GPT-5 Nano", "gpt-5-nano-2025-08-07"),
        ],
        ["Anthropic"] =
        [
            new("Claude 4.6 Opus", "claude-opus-4-6"),
            new("Claude 4.6 Sonnet", "claude-sonnet-4-6"),
            new("Claude 4.5 Haiku", "claude-haiku-4-5"),
        ],
        ["Google"] =
        [
            new("Gemini Embedding 001", "gemini-embedding-001"),
            new("Gemini 2.5 Flash Lite", "gemini-2.5-flash-lite"),
            new("Gemini 2.5 Flash", "gemini-2.5-flash"),
        ],
    };

    public static ProviderInfo DefaultProvider => Providers[0];

    public static ModelInfo DefaultModel(string providerId) => Models[providerId][0];
}
