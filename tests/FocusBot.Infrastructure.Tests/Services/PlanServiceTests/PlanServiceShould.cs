using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services.PlanServiceTests;

public class PlanServiceShould
{
    private static IPlanService BuildService(
        Mock<IFocusBotApiClient> apiClient,
        Mock<ISettingsService> settings)
    {
        return new PlanService(
            apiClient.Object,
            settings.Object,
            NullLogger<PlanService>.Instance);
    }

    [Fact]
    public async Task ReturnFreeBYOK_WhenNotAuthenticated()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(false);

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingAsync<int?>("Plan_Type")).ReturnsAsync((int?)null);
        settings.Setup(s => s.GetSettingAsync<DateTime?>("Plan_CachedAt")).ReturnsAsync((DateTime?)null);

        var sut = BuildService(apiClient, settings);

        // Act
        var plan = await sut.GetCurrentPlanAsync();

        // Assert
        plan.Should().Be(ClientPlanType.FreeBYOK);
        apiClient.Verify(a => a.GetSubscriptionStatusAsync(), Times.Never);
    }

    [Fact]
    public async Task FetchPlanFromApi_OnFirstCall()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.Setup(a => a.GetSubscriptionStatusAsync())
                 .ReturnsAsync(new ApiSubscriptionStatus("active", (int)ClientPlanType.CloudManaged, null, null));

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingAsync<int?>("Plan_Type")).ReturnsAsync((int?)null);
        settings.Setup(s => s.GetSettingAsync<DateTime?>("Plan_CachedAt")).ReturnsAsync((DateTime?)null);

        var sut = BuildService(apiClient, settings);

        // Act
        var plan = await sut.GetCurrentPlanAsync();

        // Assert
        plan.Should().Be(ClientPlanType.CloudManaged);
        apiClient.Verify(a => a.GetSubscriptionStatusAsync(), Times.Once);
    }

    [Fact]
    public async Task ReturnMemoryCachedPlan_WhenCalledTwice()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.Setup(a => a.GetSubscriptionStatusAsync())
                 .ReturnsAsync(new ApiSubscriptionStatus("active", (int)ClientPlanType.CloudBYOK, null, null));

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingAsync<int?>("Plan_Type")).ReturnsAsync((int?)null);
        settings.Setup(s => s.GetSettingAsync<DateTime?>("Plan_CachedAt")).ReturnsAsync((DateTime?)null);

        var sut = BuildService(apiClient, settings);

        // Act
        await sut.GetCurrentPlanAsync(); // first call fetches
        var plan = await sut.GetCurrentPlanAsync(); // second call uses memory cache

        // Assert
        plan.Should().Be(ClientPlanType.CloudBYOK);
        apiClient.Verify(a => a.GetSubscriptionStatusAsync(), Times.Once);
    }

    [Fact]
    public async Task RaisePlanChanged_WhenPlanChanges()
    {
        // Arrange
        var callCount = 0;
        var lastPlan = ClientPlanType.FreeBYOK;

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.SetupSequence(a => a.GetSubscriptionStatusAsync())
                 .ReturnsAsync(new ApiSubscriptionStatus("active", (int)ClientPlanType.FreeBYOK, null, null))
                 .ReturnsAsync(new ApiSubscriptionStatus("active", (int)ClientPlanType.CloudManaged, null, null));

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingAsync<int?>("Plan_Type")).ReturnsAsync((int?)null);
        settings.Setup(s => s.GetSettingAsync<DateTime?>("Plan_CachedAt")).ReturnsAsync((DateTime?)null);

        var sut = BuildService(apiClient, settings);
        sut.PlanChanged += (_, plan) =>
        {
            callCount++;
            lastPlan = plan;
        };

        // Act
        await sut.RefreshAsync(); // establishes FreeBYOK
        await sut.RefreshAsync(); // upgrades to CloudManaged → fires event

        // Assert
        callCount.Should().Be(1);
        lastPlan.Should().Be(ClientPlanType.CloudManaged);
    }

    [Fact]
    public void IdentifyCloudPlan_WhenPlanIsCloudBYOK()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        var settings = new Mock<ISettingsService>();
        var sut = BuildService(apiClient, settings);

        // Act & Assert
        sut.IsCloudPlan(ClientPlanType.CloudBYOK).Should().BeTrue();
        sut.IsCloudPlan(ClientPlanType.CloudManaged).Should().BeTrue();
        sut.IsCloudPlan(ClientPlanType.FreeBYOK).Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsTrial_WhenApiReturnsTrial()
    {
        var trialEnd = DateTime.UtcNow.AddHours(20);
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.Setup(a => a.GetSubscriptionStatusAsync())
            .ReturnsAsync(new ApiSubscriptionStatus("trial", (int)ClientPlanType.FreeBYOK, trialEnd, null));

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingAsync<int?>("Plan_Type")).ReturnsAsync((int?)null);
        settings.Setup(s => s.GetSettingAsync<DateTime?>("Plan_CachedAt")).ReturnsAsync((DateTime?)null);

        var sut = BuildService(apiClient, settings);

        var status = await sut.GetStatusAsync();
        status.Should().Be(ClientSubscriptionStatus.Trial);

        var ends = await sut.GetTrialEndsAtAsync();
        ends.Should().Be(trialEnd);
    }

    [Fact]
    public async Task GetTrialEndsAtAsync_ReturnsCachedValue_FromSecondCall()
    {
        var trialEnd = DateTime.UtcNow.AddHours(10);
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.Setup(a => a.GetSubscriptionStatusAsync())
            .ReturnsAsync(new ApiSubscriptionStatus("trial", (int)ClientPlanType.FreeBYOK, trialEnd, null));

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingAsync<int?>("Plan_Type")).ReturnsAsync((int?)null);
        settings.Setup(s => s.GetSettingAsync<DateTime?>("Plan_CachedAt")).ReturnsAsync((DateTime?)null);

        var sut = BuildService(apiClient, settings);

        await sut.GetCurrentPlanAsync();
        apiClient.Verify(a => a.GetSubscriptionStatusAsync(), Times.Once);

        var ends = await sut.GetTrialEndsAtAsync();
        ends.Should().Be(trialEnd);

        var ends2 = await sut.GetTrialEndsAtAsync();
        ends2.Should().Be(trialEnd);
        apiClient.Verify(a => a.GetSubscriptionStatusAsync(), Times.Once);
    }
}
