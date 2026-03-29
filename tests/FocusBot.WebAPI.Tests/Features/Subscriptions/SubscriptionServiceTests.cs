using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Pricing;
using FocusBot.WebAPI.Features.Subscriptions;
using FocusBot.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

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

    private static SubscriptionService CreateService(ApiDbContext db)
    {
        var hub = CreateHubMock();
        var paddle = Mock.Of<IPaddleBillingApi>();
        var logger = Mock.Of<ILogger<SubscriptionService>>();
        return new SubscriptionService(db, hub.Object, paddle, logger);
    }

    private static Mock<IHubContext<FocusHub, IFocusHubClient>> CreateHubMock()
    {
        var clients = new Mock<IHubClients<IFocusHubClient>>();
        var typedClient = new Mock<IFocusHubClient>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(typedClient.Object);
        var hub = new Mock<IHubContext<FocusHub, IFocusHubClient>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        return hub;
    }

    [Fact]
    public async Task ActivateTrialAsync_CreatesTrialSubscription()
    {
        await using var db = CreateInMemoryDb();
        var service = CreateService(db);
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

        var service = CreateService(db);
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

        var service = CreateService(db);
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

        var service = CreateService(db);
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

        var service = CreateService(db);
        var result = await service.IsSubscribedOrTrialActiveAsync(userId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandlePaddleWebhookAsync_SubscriptionCreated_SetsPlanTypeAndEnrichment()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        var json = $$"""
        {
          "event_type": "subscription.created",
          "data": {
            "id": "sub_test_1",
            "status": "active",
            "customer_id": "ctm_test",
            "custom_data": { "user_id": "{{userId}}", "plan_type": "cloud-managed" },
            "items": [{
              "price": {
                "id": "pri_1",
                "product_id": "pro_1",
                "unit_price": { "amount": "499", "currency_code": "USD" },
                "billing_cycle": { "interval": "month", "frequency": 1 },
                "custom_data": { "plan_type": "cloud-managed" }
              }
            }],
            "current_billing_period": { "ends_at": "2030-01-01T00:00:00Z" },
            "next_billed_at": "2030-01-01T00:00:00Z"
          }
        }
        """;

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        await service.HandlePaddleWebhookAsync(doc.RootElement);

        var row = await db.Subscriptions.SingleAsync(s => s.UserId == userId);
        row.Status.Should().Be("active");
        row.PlanType.Should().Be(PlanType.CloudManaged);
        row.PaddleSubscriptionId.Should().Be("sub_test_1");
        row.PaddleCustomerId.Should().Be("ctm_test");
        row.PaddlePriceId.Should().Be("pri_1");
        row.PaddleProductId.Should().Be("pro_1");
        row.CurrencyCode.Should().Be("USD");
        row.UnitAmountMinor.Should().Be(499);
        row.BillingInterval.Should().Be("month");
        row.CurrentPeriodEndsAtUtc.Should().NotBeNull();
        row.NextBilledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatusAsync_IncludesNextBilledAt()
    {
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var next = DateTime.UtcNow.AddDays(30);
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Status = "active",
            PlanType = PlanType.CloudBYOK,
            NextBilledAtUtc = next,
            PaddleSubscriptionId = "sub_x"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var status = await service.GetStatusAsync(userId);

        status.NextBilledAtUtc.Should().Be(next);
    }
}
