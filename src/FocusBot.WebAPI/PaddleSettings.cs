namespace FocusBot.WebAPI;

/// <summary>
/// Configuration for Paddle Billing (API key, webhook secret, client token for Paddle.js).
/// </summary>
public sealed class PaddleSettings
{
    public const string SectionName = "Paddle";

    /// <summary>Paddle Billing API base URL (e.g. https://sandbox-api.paddle.com or https://api.paddle.com).</summary>
    public string ApiBase { get; set; } = "";

    /// <summary>Server-side API key (Bearer). Never expose to browsers.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Webhook signing secret from Paddle dashboard (Notifications).</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Client-side token for Paddle.js initialization (public).</summary>
    public string ClientToken { get; set; } = "";

    /// <summary>When true, web clients should call Paddle.Environment.set('sandbox').</summary>
    public bool IsSandbox { get; set; } = true;

    /// <summary>
    /// Paddle product id (<c>pro_...</c>) whose prices are exposed via <c>GET /pricing</c>.
    /// Set per environment (user secrets, env vars, or appsettings) so sandbox and production catalogs stay isolated.
    /// </summary>
    public string CatalogProductId { get; set; } = "";
}
