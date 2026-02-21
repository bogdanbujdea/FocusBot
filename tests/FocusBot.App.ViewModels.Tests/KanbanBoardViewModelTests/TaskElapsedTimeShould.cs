using FocusBot.Core.Interfaces;
using Moq;
using TaskStatus = FocusBot.Core.Entities.TaskStatus;

namespace FocusBot.App.ViewModels.Tests.KanbanBoardViewModelTests;

public class TaskElapsedTimeShould
{
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
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Resumed task");
        await ctx.Repo.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        await ctx.Repo.UpdateElapsedTimeAsync(task.TaskId, 3661);
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var navMock = new Mock<INavigationService>();
        var openAIMock = new Mock<IOpenAIService>();
        var settingsMock = new Mock<ISettingsService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var vm = new KanbanBoardViewModel(ctx.Repo, monitorMock.Object, timeTrackingMock.Object, navMock.Object, openAIMock.Object, settingsMock.Object, focusScoreMock.Object);
        await Task.Delay(150);

        // Assert
        vm.TaskElapsedTime.Should().Be("01:01:01");
    }

    [Fact]
    public async Task ResetToZero_WhenNoTaskInProgress()
    {
        // Arrange
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var navMock = new Mock<INavigationService>();
        var openAIMock = new Mock<IOpenAIService>();
        var settingsMock = new Mock<ISettingsService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var vm = new KanbanBoardViewModel(ctx.Repo, monitorMock.Object, timeTrackingMock.Object, navMock.Object, openAIMock.Object, settingsMock.Object, focusScoreMock.Object);
        await Task.Delay(150);

        // Assert
        vm.TaskElapsedTime.Should().Be("00:00:00");
    }

    [Fact]
    public async Task AccumulateAcrossSessions_WhenTaskIsRestarted()
    {
        // Arrange
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Accumulated task");
        await ctx.Repo.SetStatusToAsync(task.TaskId, TaskStatus.InProgress);
        await ctx.Repo.UpdateElapsedTimeAsync(task.TaskId, 60);
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var navMock = new Mock<INavigationService>();
        var openAIMock = new Mock<IOpenAIService>();
        var settingsMock = new Mock<ISettingsService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var vm = new KanbanBoardViewModel(ctx.Repo, monitorMock.Object, timeTrackingMock.Object, navMock.Object, openAIMock.Object, settingsMock.Object, focusScoreMock.Object);
        await Task.Delay(150);

        vm.TaskElapsedTime.Should().Be("00:01:00");

        // Act - tick 5 more seconds
        for (var i = 0; i < 5; i++)
            timeTrackingMock.Raise(m => m.Tick += null, timeTrackingMock.Object, EventArgs.Empty);

        vm.TaskElapsedTime.Should().Be("00:01:05");

        // Act - stop task (persists), then start again
        await vm.MoveToToDoCommand.ExecuteAsync(task.TaskId);
        await vm.MoveToInProgressCommand.ExecuteAsync(task.TaskId);
        await Task.Delay(150);

        // Assert - should show accumulated time from previous session
        vm.TaskElapsedTime.Should().Be("00:01:05");

        // Act - one more tick
        timeTrackingMock.Raise(m => m.Tick += null, timeTrackingMock.Object, EventArgs.Empty);

        // Assert
        vm.TaskElapsedTime.Should().Be("00:01:06");
    }
}
