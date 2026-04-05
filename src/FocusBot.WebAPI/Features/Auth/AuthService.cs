using System.Security.Claims;
using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Clients;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Features.Auth;

/// <summary>
/// Handles user auto-provisioning and profile retrieval from JWT claims.
/// </summary>
public class AuthService(ApiDbContext db, ClientService clientService)
{
    public async Task<ProvisionedUserResult> GetOrProvisionUserAsync(
        ClaimsPrincipal principal,
        string? clientFingerprint,
        string? clientName,
        string? appVersion,
        string? platform,
        string? remoteIpAddress,
        CancellationToken ct = default
    )
    {
        var sub =
            principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT missing sub claim");

        var userId = Guid.Parse(sub);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            var email =
                principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue("email")
                ?? string.Empty;

            user = new User
            {
                Id = userId,
                Email = email,
                CreatedAtUtc = DateTime.UtcNow,
            };

            var trial = new Subscription
            {
                UserId = userId,
                Status = SubscriptionStatus.Trial,
                PlanType = PlanType.TrialFullAccess,
                CurrentPeriodEndsAtUtc = DateTime.UtcNow.AddHours(24),
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
        }

        Guid? clientId = null;
        if (
            !string.IsNullOrWhiteSpace(clientFingerprint)
            && clientFingerprint.Length <= 100
        )
        {
            var registeredClient = await clientService.RegisterAsync(
                userId,
                new RegisterClientRequest(
                    ClientType.Desktop,
                    ClientHost.Windows,
                    string.IsNullOrWhiteSpace(clientName) ? "Desktop App" : clientName,
                    clientFingerprint,
                    appVersion,
                    string.IsNullOrWhiteSpace(platform) ? "Windows" : platform
                ),
                remoteIpAddress,
                ct
            );
            clientId = registeredClient.Id;
        }

        return new ProvisionedUserResult(user, clientId);
    }
}

public sealed record ProvisionedUserResult(User User, Guid? ClientId);
