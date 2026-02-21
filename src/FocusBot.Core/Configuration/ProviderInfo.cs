namespace FocusBot.Core.Configuration;

/// <summary>
/// Display and identifier for an LLM provider in the settings UI.
/// </summary>
public record ProviderInfo(string DisplayName, string ProviderId, string ApiKeyUrl);
