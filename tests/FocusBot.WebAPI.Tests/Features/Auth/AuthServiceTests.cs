using System.Security.Claims;
using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Tests.Features.Auth;

public class AuthServiceTests
{
    private static ApiDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApiDbContext(options);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string email)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task GetOrProvisionUserAsync_CreatesNewUserAndTrial_WhenNotExists()
    {
        await using var db = CreateInMemoryDb();
        var service = new AuthService(db);
        var userId = Guid.NewGuid();

        var user = await service.GetOrProvisionUserAsync(
            CreatePrincipal(userId, "test@example.com")
        );

        user.Id.Should().Be(userId);
        user.Email.Should().Be("test@example.com");
        (await db.Users.CountAsync()).Should().Be(1);

        var subscription = await db.Subscriptions.SingleAsync();
        subscription.UserId.Should().Be(userId);
        subscription.Status.Should().Be(SubscriptionStatus.Trial);
        subscription.PlanType.Should().Be(PlanType.TrialFullAccess);
        subscription
            .CurrentPeriodEndsAtUtc.Should()
            .BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetOrProvisionUserAsync_ReturnsExistingUser_WhenAlreadyExists()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = "existing@example.com" });
        db.Subscriptions.Add(
            new Subscription
            {
                UserId = userId,
                Status = SubscriptionStatus.Trial,
                PlanType = PlanType.TrialFullAccess,
                CurrentPeriodEndsAtUtc = DateTime.UtcNow.AddHours(20),
            }
        );
        await db.SaveChangesAsync();

        var service = new AuthService(db);
        var user = await service.GetOrProvisionUserAsync(
            CreatePrincipal(userId, "existing@example.com")
        );

        user.Id.Should().Be(userId);
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.Subscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetOrProvisionUserAsync_Throws_WhenSubClaimMissing()
    {
        await using var db = CreateInMemoryDb();
        var service = new AuthService(db);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetOrProvisionUserAsync(principal)
        );
    }
}
