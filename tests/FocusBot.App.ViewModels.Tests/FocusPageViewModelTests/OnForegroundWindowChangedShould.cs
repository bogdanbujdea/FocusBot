using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class OnForegroundWindowChangedShould
{
    [Fact]
    public async Task SetProcessNameAndWindowTitle_When_Foreground_Changes()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
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
        var eventArgs = new ForegroundWindowChangedEventArgs
        {
            ProcessName = "devenv",
            WindowTitle = "MyFile.cs",
        };

        // Act
        monitorMock.Raise(m => m.ForegroundWindowChanged += null, monitorMock.Object, eventArgs);

        // Assert
        vm.CurrentProcessName.Should().Be("devenv");
        vm.CurrentWindowTitle.Should().Be("MyFile.cs");
    }
}
