using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class InitializeShould
{
    [Fact]
    public async Task LoadActiveSession_WhenApiReturnsActiveSession()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var id = Guid.NewGuid();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(id, "In progress task", null, null, DateTime.UtcNow, null));

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync(session);

        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());

        var statusBar = new FocusStatusViewModel(orchestratorMock.Object);
        var vm = new FocusPageViewModel(
            navMock.Object,
            settingsMock.Object,
            orchestratorMock.Object,
            Mock.Of<IFocusHubClient>(),
            accountVm,
            statusBar);

        await Task.Delay(200);

        vm.ActiveSession.Should().NotBeNull();
        vm.ActiveSession!.SessionTitle.Should().Be("In progress task");
    }
}
