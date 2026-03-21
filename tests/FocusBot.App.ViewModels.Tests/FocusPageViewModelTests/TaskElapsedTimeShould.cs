using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class TaskElapsedTimeShould
{
    [Fact]
    public async Task IncrementEverySecond_WhenTimerTicks()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddSessionAsync("Tracked task");
        await ctx.Repo.SetActiveAsync(task.SessionId);
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            navMock.Object,
            Mock.Of<IClassificationService>(),
            settingsMock.Object,
            Mock.Of<ILocalSessionTracker>(),
            Mock.Of<IAlignmentCacheRepository>(),
            Mock.Of<IFocusBotApiClient>(),
            accountVm);
        await Task.Delay(150);

        // Act
        monitorMock.Raise(m => m.Tick += null, monitorMock.Object, EventArgs.Empty);

        // Assert
        vm.SessionElapsedTime.Should().Be("00:00:01");

        // Act
        monitorMock.Raise(m => m.Tick += null, monitorMock.Object, EventArgs.Empty);

        // Assert
        vm.SessionElapsedTime.Should().Be("00:00:02");
    }

    [Fact]
    public async Task StartFromStoredValue_WhenTaskIsLoaded()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddSessionAsync("Resumed task");
        await ctx.Repo.SetActiveAsync(task.SessionId);
        await ctx.Repo.UpdateElapsedTimeAsync(task.SessionId, 3661);
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            navMock.Object,
            Mock.Of<IClassificationService>(),
            settingsMock.Object,
            Mock.Of<ILocalSessionTracker>(),
            Mock.Of<IAlignmentCacheRepository>(),
            Mock.Of<IFocusBotApiClient>(),
            accountVm);
        await Task.Delay(150);

        // Assert
        vm.SessionElapsedTime.Should().Be("01:01:01");
    }

    [Fact]
    public async Task ResetToZero_WhenNoTaskInProgress()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            navMock.Object,
            Mock.Of<IClassificationService>(),
            settingsMock.Object,
            Mock.Of<ILocalSessionTracker>(),
            Mock.Of<IAlignmentCacheRepository>(),
            Mock.Of<IFocusBotApiClient>(),
            accountVm);
        await Task.Delay(150);

        // Assert
        vm.SessionElapsedTime.Should().Be("00:00:00");
    }
}
