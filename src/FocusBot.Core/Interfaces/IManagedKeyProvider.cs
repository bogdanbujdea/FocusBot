namespace FocusBot.Core.Interfaces;

/// <summary>
/// Provides the managed API key for subscribed users.
/// </summary>
/// <remarks>
/// This interface abstracts the key source, allowing easy migration from
/// embedded key (MVP) to server-fetched key (production).
/// </remarks>
public interface IManagedKeyProvider
{
    /// <summary>
    /// Gets the managed API key.
    /// Returns null if the key is unavailable.
    /// </summary>
    Task<string?> GetApiKeyAsync();

    /// <summary>
    /// Gets the provider ID for the managed key (e.g., "OpenAi").
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Gets the model ID for the managed key (e.g., "gpt-4o-mini").
    /// </summary>
    string ModelId { get; }
}
