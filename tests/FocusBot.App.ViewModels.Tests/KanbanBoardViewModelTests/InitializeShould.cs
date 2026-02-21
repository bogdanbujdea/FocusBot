using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.App.ViewModels.Tests.KanbanBoardViewModelTests;

public class InitializeShould
{
    [Fact]
    public async Task StartWindowMonitor_When_ThereAreTasksInProgress()
    {
        // Arrange
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("In progress task");
        await ctx.Repo.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var openAIMock = new Mock<IOpenAIService>();
        var settingsMock = new Mock<ISettingsService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var vm = new KanbanBoardViewModel(ctx.Repo, monitorMock.Object, timeTrackingMock.Object, navMock.Object, openAIMock.Object, settingsMock.Object, focusScoreMock.Object);

        // Act

        // Assert
        monitorMock.Verify(x => x.Start(), Times.Once);
        timeTrackingMock.Verify(x => x.Start(), Times.Once);
    }

    [Fact]
    public async Task ClearProcessNameAndWindowTitle_When_MonitoringIsStopped()
    {
        // Arrange
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Task to stop");
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var openAIMock = new Mock<IOpenAIService>();
        var settingsMock = new Mock<ISettingsService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var vm = new KanbanBoardViewModel(ctx.Repo, monitorMock.Object, timeTrackingMock.Object, navMock.Object, openAIMock.Object, settingsMock.Object, focusScoreMock.Object);

        await vm.MoveToInProgressCommand.ExecuteAsync(task.TaskId);
        var eventArgs = new ForegroundWindowChangedEventArgs
        {
            ProcessName = "code",
            WindowTitle = "Editor",
        };
        monitorMock.Raise(m => m.ForegroundWindowChanged += null, monitorMock.Object, eventArgs);
        vm.CurrentProcessName.Should().Be("code");
        vm.CurrentWindowTitle.Should().Be("Editor");

        // Act
        await vm.MoveToToDoCommand.ExecuteAsync(task.TaskId);

        // Assert
        vm.CurrentProcessName.Should().BeEmpty();
        vm.CurrentWindowTitle.Should().BeEmpty();
    }
}
