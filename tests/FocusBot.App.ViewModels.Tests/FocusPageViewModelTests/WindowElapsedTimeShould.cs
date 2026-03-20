using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;
namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class WindowElapsedTimeShould
{
    [Fact]
    public async Task ResetToZero_WhenWindowChanges()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Tracked task");
        await ctx.Repo.SetActiveAsync(task.TaskId);
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            timeTrackingMock.Object,
            idleDetectionMock.Object,
            navMock.Object,
            Mock.Of<IClassificationService>(),
            settingsMock.Object,
            Mock.Of<ILocalSessionTracker>(),
            Mock.Of<IAlignmentCacheRepository>(),
            Mock.Of<IFocusBotApiClient>(),
            accountVm);
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
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Tracked task");
        await ctx.Repo.SetActiveAsync(task.TaskId);
        var monitorMock = new Mock<IWindowMonitorService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            timeTrackingMock.Object,
            idleDetectionMock.Object,
            navMock.Object,
            Mock.Of<IClassificationService>(),
            settingsMock.Object,
            Mock.Of<ILocalSessionTracker>(),
            Mock.Of<IAlignmentCacheRepository>(),
            Mock.Of<IFocusBotApiClient>(),
            accountVm);
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
