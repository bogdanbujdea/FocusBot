using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class InitializeShould
{
    [Fact]
    public async Task StartWindowMonitor_When_ThereAreTasksInProgress()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("In progress task");
        await ctx.Repo.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var trialMock = new Mock<ITrialService>();
        var distractionMock = new Mock<IDistractionDetectorService>();
        var distractionRepoMock = new Mock<IDistractionEventRepository>();
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
            distractionRepoMock.Object,
            dailyAnalyticsMock.Object,
            alignmentCacheMock.Object,
            taskSummaryMock.Object);

        // Act

        // Assert
        monitorMock.Verify(x => x.Start(), Times.Once);
        timeTrackingMock.Verify(x => x.Start(), Times.Once);
    }
}
