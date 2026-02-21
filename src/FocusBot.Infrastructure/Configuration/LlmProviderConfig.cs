using FocusBot.Core.Configuration;
using LlmTornado.Code;

namespace FocusBot.Infrastructure.Configuration;

/// <summary>
/// Maps provider IDs from settings to LLMTornado provider enum. Add new providers here when extending Core's LlmProviderConfig.
/// </summary>
public static class LlmProviderConfig
{
    public static LLmProviders ToLlmProvider(string providerId) => providerId switch
    {
        "OpenAi" => LLmProviders.OpenAi,
        "Anthropic" => LLmProviders.Anthropic,
        "Google" => LLmProviders.Google,
        _ => LLmProviders.OpenAi,
    };
}
