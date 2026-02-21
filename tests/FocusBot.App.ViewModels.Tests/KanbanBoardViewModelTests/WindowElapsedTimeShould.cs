using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.App.ViewModels.Tests.KanbanBoardViewModelTests;

public class WindowElapsedTimeShould
{
    [Fact]
    public async Task ResetToZero_WhenWindowChanges()
    {
        // Arrange
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Tracked task");
        await ctx.Repo.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var navMock = new Mock<INavigationService>();
        var openAIMock = new Mock<IOpenAIService>();
        var settingsMock = new Mock<ISettingsService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var vm = new KanbanBoardViewModel(ctx.Repo, monitorMock.Object, timeTrackingMock.Object, navMock.Object, openAIMock.Object, settingsMock.Object, focusScoreMock.Object);
        await Task.Delay(150);

        timeTrackingMock.Raise(m => m.Tick += null, timeTrackingMock.Object, EventArgs.Empty);
        timeTrackingMock.Raise(m => m.Tick += null, timeTrackingMock.Object, EventArgs.Empty);
        vm.WindowElapsedTime.Should().Be("00:00:02");

        // Act
        monitorMock.Raise(m => m.ForegroundWindowChanged += null, monitorMock.Object, new ForegroundWindowChangedEventArgs
        {
            ProcessName = "other",
            WindowTitle = "Other window"
        });

        // Assert
        vm.WindowElapsedTime.Should().Be("00:00:00");
    }

    [Fact]
    public async Task IncrementEverySecond_WhenTimerTicks()
    {
        // Arrange
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Tracked task");
        await ctx.Repo.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var navMock = new Mock<INavigationService>();
        var openAIMock = new Mock<IOpenAIService>();
        var settingsMock = new Mock<ISettingsService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var vm = new KanbanBoardViewModel(ctx.Repo, monitorMock.Object, timeTrackingMock.Object, navMock.Object, openAIMock.Object, settingsMock.Object, focusScoreMock.Object);
        await Task.Delay(150);

        // Act
        timeTrackingMock.Raise(m => m.Tick += null, timeTrackingMock.Object, EventArgs.Empty);

        // Assert
        vm.WindowElapsedTime.Should().Be("00:00:01");

        // Act
        timeTrackingMock.Raise(m => m.Tick += null, timeTrackingMock.Object, EventArgs.Empty);

        // Assert
        vm.WindowElapsedTime.Should().Be("00:00:02");
    }
}
