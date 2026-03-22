namespace FocusBot.WebAPI.Data.Entities;

/// <summary>
/// Represents a registered software client (desktop app or browser extension install).
/// </summary>
public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Desktop vs extension.</summary>
    public ClientType ClientType { get; set; }

    /// <summary>Runtime host (Windows app, Chrome, Edge, etc.).</summary>
    public ClientHost Host { get; set; } = ClientHost.Unknown;

    /// <summary>User-visible or hostname-derived label.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stable identifier persisted on the client. Used to detect re-registration
    /// of the same install after uninstall/reinstall.
    /// </summary>
    public string Fingerprint { get; set; } = string.Empty;

    public string? AppVersion { get; set; }
    public string? Platform { get; set; }

    /// <summary>Observed IP from the last register or heartbeat (server-derived).</summary>
    public string? IpAddress { get; set; }

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

/// <summary>Client categories. Web is not included — the web app is not a registered client.</summary>
public enum ClientType
{
    Desktop = 1,
    Extension = 2,
}

/// <summary>Where the client runs.</summary>
public enum ClientHost
{
    Unknown = 0,
    Windows = 1,
    Chrome = 2,
    Edge = 3,
}
