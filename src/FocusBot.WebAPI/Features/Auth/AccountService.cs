using FocusBot.WebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Features.Auth;

/// <summary>
/// Handles account-level operations such as data deletion.
/// </summary>
public class AccountService(ApiDbContext db)
{
    /// <summary>
    /// Deletes all user data: sessions, devices, subscriptions, classification caches, and the user record.
    /// </summary>
    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var sessions = await db.Sessions.Where(s => s.UserId == userId).ToListAsync(ct);
        db.Sessions.RemoveRange(sessions);

        var devices = await db.Devices.Where(d => d.UserId == userId).ToListAsync(ct);
        db.Devices.RemoveRange(devices);

        var caches = await db.ClassificationCaches.Where(c => c.UserId == userId).ToListAsync(ct);
        db.ClassificationCaches.RemoveRange(caches);

        var subscription = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (subscription is not null)
            db.Subscriptions.Remove(subscription);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is not null)
            db.Users.Remove(user);

        await db.SaveChangesAsync(ct);
    }
}
