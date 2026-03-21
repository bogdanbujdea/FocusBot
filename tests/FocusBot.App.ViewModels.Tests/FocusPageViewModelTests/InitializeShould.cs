using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class InitializeShould
{
    [Fact]
    public async Task LoadActiveSession_WhenThereAreTasksInProgress()
    {
        // Arrange
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var task = await ctx.Repo.AddSessionAsync("In progress task");
        await ctx.Repo.SetActiveAsync(task.SessionId);

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());

        // Act
        var vm = new FocusPageViewModel(
            ctx.Repo,
            navMock.Object,
            settingsMock.Object,
            orchestratorMock.Object,
            accountVm);

        // Wait for async initialization
        await Task.Delay(150);

        // Assert
        vm.ActiveSession.Should().NotBeNull();
        vm.ActiveSession!.SessionTitle.Should().Be("In progress task");
    }
}
