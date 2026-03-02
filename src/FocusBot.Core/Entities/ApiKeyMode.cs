namespace FocusBot.Core.Entities;

/// <summary>
/// Specifies how the app obtains the API key for AI services.
/// </summary>
public enum ApiKeyMode
{
    /// <summary>
    /// User provides their own API key from their AI provider account.
    /// </summary>
    Own,

    /// <summary>
    /// User subscribes and uses a managed API key provided by the app.
    /// </summary>
    Managed,

    /// <summary>
    /// User is in the 24-hour free trial period using the managed key.
    /// </summary>
    Trial
}
