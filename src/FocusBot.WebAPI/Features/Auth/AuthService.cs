using System.Security.Claims;
using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Features.Auth;

/// <summary>
/// Handles user auto-provisioning and profile retrieval from JWT claims.
/// </summary>
public class AuthService(ApiDbContext db)
{
    public async Task<User> GetOrProvisionUserAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("JWT missing sub claim");

        var userId = Guid.Parse(sub);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is not null)
            return user;

        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email")
                    ?? string.Empty;

        user = new User
        {
            Id = userId,
            Email = email,
            CreatedAtUtc = DateTime.UtcNow
        };

        var trial = new Subscription
        {
            UserId = userId,
            Status = SubscriptionStatus.Trial,
            PlanType = PlanType.TrialFullAccess,
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(24),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        db.Users.Add(user);
        db.Subscriptions.Add(trial);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(user).State = EntityState.Detached;
            db.Entry(trial).State = EntityState.Detached;
            user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        }

        return user;
    }
}
