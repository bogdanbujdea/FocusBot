using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.WebAPI.Tests.Features.Subscriptions;

public class SubscriptionServiceTests
{
    private static ApiDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApiDbContext(options);
    }

    [Fact]
    public async Task ActivateTrialAsync_CreatesTrialSubscription()
    {
        await using var db = CreateInMemoryDb();
        var service = new SubscriptionService(db);
        var userId = Guid.NewGuid();

        var result = await service.ActivateTrialAsync(userId);

        result.Should().NotBeNull();
        result!.Status.Should().Be("trial");
        result.TrialEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
        (await db.Subscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ActivateTrialAsync_ReturnsNull_WhenTrialAlreadyActivated()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = "trial",
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        var result = await service.ActivateTrialAsync(userId);

        result.Should().BeNull();
        (await db.Subscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task IsSubscribedOrTrialActiveAsync_ReturnsTrue_ForActiveSubscription()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = "active",
            PaddleSubscriptionId = "sub_123"
        });
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        var result = await service.IsSubscribedOrTrialActiveAsync(userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSubscribedOrTrialActiveAsync_ReturnsTrue_ForActiveTrial()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = "trial",
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(12)
        });
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        var result = await service.IsSubscribedOrTrialActiveAsync(userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSubscribedOrTrialActiveAsync_ReturnsFalse_ForExpiredTrial()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = "trial",
            TrialEndsAtUtc = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        var result = await service.IsSubscribedOrTrialActiveAsync(userId);

        result.Should().BeFalse();
    }
}
