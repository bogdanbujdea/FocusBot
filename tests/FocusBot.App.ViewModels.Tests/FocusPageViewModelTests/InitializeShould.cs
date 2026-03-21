using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class InitializeShould
{
    [Fact]
    public async Task StartWindowMonitor_When_ThereAreTasksInProgress()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("In progress task");
        await ctx.Repo.SetActiveAsync(task.TaskId);
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
            accountVm
        );

        // Act

        // Assert
        monitorMock.Verify(x => x.Start(), Times.Once);
    }
}
