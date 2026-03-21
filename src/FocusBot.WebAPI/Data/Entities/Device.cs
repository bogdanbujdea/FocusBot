namespace FocusBot.WebAPI.Data.Entities;

/// <summary>
/// Represents a registered client device (desktop app or browser extension install).
/// </summary>
public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Type of device: Desktop or Extension.</summary>
    public DeviceType DeviceType { get; set; }

    /// <summary>User-visible or hostname-derived label for the device.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stable identifier persisted on the client. Used to detect re-registration
    /// of the same physical install after uninstall/reinstall.
    /// </summary>
    public string Fingerprint { get; set; } = string.Empty;

    public string? AppVersion { get; set; }
    public string? Platform { get; set; }

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

/// <summary>Client device categories. Web is not included — the web app is not a registered device.</summary>
public enum DeviceType
{
    Desktop = 1,
    Extension = 2,
}
