using FocusBot.Core.Interfaces;
using Moq;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class TaskElapsedTimeShould
{
    [Fact]
    public async Task IncrementEverySecond_WhenTimerTicks()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Tracked task");
        await ctx.Repo.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var trialMock = new Mock<ITrialService>();
        var distractionMock = new Mock<IDistractionDetectorService>();
        var dailyAnalyticsMock = new Mock<IDailyAnalyticsService>();
        var alignmentCacheMock = new Mock<IAlignmentCacheRepository>();
        var taskSummaryMock = new Mock<ITaskSummaryService>();
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            timeTrackingMock.Object,
            idleDetectionMock.Object,
            navMock.Object,
            llmMock.Object,
            settingsMock.Object,
            focusScoreMock.Object,
            trialMock.Object,
            distractionMock.Object,
            dailyAnalyticsMock.Object,
            alignmentCacheMock.Object,
            taskSummaryMock.Object);
        await Task.Delay(150);

        // Act
        timeTrackingMock.Raise(m => m.Tick += null, timeTrackingMock.Object, EventArgs.Empty);

        // Assert
        vm.TaskElapsedTime.Should().Be("00:00:01");

        // Act
        timeTrackingMock.Raise(m => m.Tick += null, timeTrackingMock.Object, EventArgs.Empty);

        // Assert
        vm.TaskElapsedTime.Should().Be("00:00:02");
    }

    [Fact]
    public async Task StartFromStoredValue_WhenTaskIsLoaded()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Resumed task");
        await ctx.Repo.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        await ctx.Repo.UpdateElapsedTimeAsync(task.TaskId, 3661);
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var trialMock = new Mock<ITrialService>();
        var distractionMock = new Mock<IDistractionDetectorService>();
        var dailyAnalyticsMock = new Mock<IDailyAnalyticsService>();
        var alignmentCacheMock = new Mock<IAlignmentCacheRepository>();
        var taskSummaryMock = new Mock<ITaskSummaryService>();
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            timeTrackingMock.Object,
            idleDetectionMock.Object,
            navMock.Object,
            llmMock.Object,
            settingsMock.Object,
            focusScoreMock.Object,
            trialMock.Object,
            distractionMock.Object,
            dailyAnalyticsMock.Object,
            alignmentCacheMock.Object,
            taskSummaryMock.Object);
        await Task.Delay(150);

        // Assert
        vm.TaskElapsedTime.Should().Be("01:01:01");
    }

    [Fact]
    public async Task ResetToZero_WhenNoTaskInProgress()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var trialMock = new Mock<ITrialService>();
        var distractionMock = new Mock<IDistractionDetectorService>();
        var dailyAnalyticsMock = new Mock<IDailyAnalyticsService>();
        var alignmentCacheMock = new Mock<IAlignmentCacheRepository>();
        var taskSummaryMock = new Mock<ITaskSummaryService>();
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            timeTrackingMock.Object,
            idleDetectionMock.Object,
            navMock.Object,
            llmMock.Object,
            settingsMock.Object,
            focusScoreMock.Object,
            trialMock.Object,
            distractionMock.Object,
            dailyAnalyticsMock.Object,
            alignmentCacheMock.Object,
            taskSummaryMock.Object);
        await Task.Delay(150);

        // Assert
        vm.TaskElapsedTime.Should().Be("00:00:00");
    }
}
