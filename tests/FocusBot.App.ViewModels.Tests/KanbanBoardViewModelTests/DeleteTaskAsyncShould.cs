using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.KanbanBoardViewModelTests;

public class DeleteTaskAsyncShould
{
    [Fact]
    public async Task RefreshAnalytics_AfterTaskDeleted()
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

        // Create a task
        var task = await ctx.Repo.AddTaskAsync("Test task");
        await Task.Delay(10);

        // Act
        await vm.DeleteTaskCommand.ExecuteAsync(task.TaskId);

        // Assert
        dailyAnalyticsMock.Verify(d => d.ReloadTodayFromDbAsync(It.IsAny<CancellationToken>()), Times.Once);
        distractionRepoMock.Verify(d => d.DeleteDistractionEventsForTaskAsync(task.TaskId, It.IsAny<CancellationToken>()), Times.Once);
        vm.ToDoTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task ShowEmptyState_WhenLastTaskDeleted()
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

        dailyAnalyticsMock
            .Setup(s => s.GetTodaySummaryAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.DTOs.DailyFocusSummary?)null);

        var alignmentCacheMock = new Mock<IAlignmentCacheRepository>();
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

        // Create and delete a task
        var task = await ctx.Repo.AddTaskAsync("Test task");
        await Task.Delay(10);
        await vm.DeleteTaskCommand.ExecuteAsync(task.TaskId);

        // Assert
        vm.ToDoTasks.Should().BeEmpty();
        vm.HasTodayAnalytics.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDistractionEvents_WhenTaskDeleted()
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

        // Create and delete a task
        var task = await ctx.Repo.AddTaskAsync("Test task");
        await Task.Delay(10);
        await vm.DeleteTaskCommand.ExecuteAsync(task.TaskId);

        // Assert
        distractionRepoMock.Verify(
            d => d.DeleteDistractionEventsForTaskAsync(task.TaskId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
