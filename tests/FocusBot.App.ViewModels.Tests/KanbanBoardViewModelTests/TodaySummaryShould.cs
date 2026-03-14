using FocusBot.Core.DTOs;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.KanbanBoardViewModelTests;

public class TodaySummaryShould
{
    [Fact]
    public async Task PopulateTodayProperties_WhenSummaryIsAvailable()
    {
        // Arrange
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var trialMock = new Mock<ITrialService>();
        var distractionDetectorMock = new Mock<IDistractionDetectorService>();
        var distractionRepoMock = new Mock<IDistractionEventRepository>();
        var dailyAnalyticsMock = new Mock<IDailyAnalyticsService>();
        var alignmentCacheMock = new Mock<IAlignmentCacheRepository>();

        var analyticsDate = DateOnly.FromDateTime(DateTime.Now);

        dailyAnalyticsMock
            .Setup(s => s.GetTodaySummaryAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyFocusSummary
            {
                AnalyticsDateLocal = analyticsDate,
                FocusScoreBucket = 7,
                FocusedTime = TimeSpan.FromSeconds(120),
                DistractedTime = TimeSpan.FromSeconds(30),
                DistractionCount = 3,
                AverageDistractionDuration = TimeSpan.FromSeconds(10)
            });

        var vm = new KanbanBoardViewModel(
            ctx.Repo,
            monitorMock.Object,
            timeTrackingMock.Object,
            idleDetectionMock.Object,
            navMock.Object,
            llmMock.Object,
            settingsMock.Object,
            focusScoreMock.Object,
            trialMock.Object,
            distractionDetectorMock.Object,
            distractionRepoMock.Object,
            dailyAnalyticsMock.Object,
            alignmentCacheMock.Object);

        // Act
        // LoadBoardAsync is invoked from the constructor; give it time to complete.
        await Task.Delay(10);

        // Assert
        vm.HasTodayAnalytics.Should().BeTrue();
        vm.TodayFocusScoreBucket.Should().Be(7);
        vm.TodayDistractionCount.Should().Be(3);
        vm.TodayFocusedTimeText.Should().Be("00:02:00");
        vm.TodayDistractedTimeText.Should().Be("00:00:30");
        vm.TodayAverageDistractionCostText.Should().Be("00:00:10");
        vm.TodayDateLabel.Should().NotBeNullOrEmpty();
        vm.TodayFocusedPercent.Should().BeApproximately(120d / 150d, 0.0001);
        vm.TodayDistractedPercent.Should().BeApproximately(30d / 150d, 0.0001);
        vm.TodayUnclearPercent.Should().Be(0);
    }

    [Fact]
    public async Task HideTodaySummary_WhenNoDataForToday()
    {
        // Arrange
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var trialMock = new Mock<ITrialService>();
        var distractionDetectorMock = new Mock<IDistractionDetectorService>();
        var distractionRepoMock = new Mock<IDistractionEventRepository>();
        var dailyAnalyticsMock = new Mock<IDailyAnalyticsService>();
        var alignmentCacheMock = new Mock<IAlignmentCacheRepository>();

        dailyAnalyticsMock
            .Setup(s => s.GetTodaySummaryAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyFocusSummary?)null);

        var vm = new KanbanBoardViewModel(
            ctx.Repo,
            monitorMock.Object,
            timeTrackingMock.Object,
            idleDetectionMock.Object,
            navMock.Object,
            llmMock.Object,
            settingsMock.Object,
            focusScoreMock.Object,
            trialMock.Object,
            distractionDetectorMock.Object,
            distractionRepoMock.Object,
            dailyAnalyticsMock.Object,
            alignmentCacheMock.Object);

        // Act
        await Task.Delay(10);

        // Assert
        vm.HasTodayAnalytics.Should().BeFalse();
        vm.TodayDistractionCount.Should().Be(0);
        vm.TodayFocusedTimeText.Should().Be("00:00:00");
        vm.TodayDistractedTimeText.Should().Be("00:00:00");
        vm.TodayAverageDistractionCostText.Should().Be("—");
        vm.TodayDateLabel.Should().BeEmpty();
        vm.TodayFocusedPercent.Should().Be(0);
        vm.TodayUnclearPercent.Should().Be(0);
        vm.TodayDistractedPercent.Should().Be(0);
    }
}

