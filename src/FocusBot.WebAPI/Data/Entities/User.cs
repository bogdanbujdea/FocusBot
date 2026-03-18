namespace FocusBot.WebAPI.Data.Entities;

/// <summary>
/// Application user, auto-provisioned from Supabase JWT claims on first authenticated request.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
