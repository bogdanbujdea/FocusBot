using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class ReloadBoardShould
{
    [Fact]
    public async Task ReloadBoardAsync_LoadsActiveSession_AfterInitialLoadHadNone()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var id = Guid.NewGuid();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(id, "Signed in later", null, null, DateTime.UtcNow, null));

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock
            .SetupSequence(o => o.LoadActiveSessionAsync())
            .ReturnsAsync((UserSession?)null)
            .ReturnsAsync(session);

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
            new StubPlanService(),
            accountVm,
            statusBar);

        await Task.Delay(200);

        vm.ActiveSession.Should().BeNull();

        await vm.ReloadBoardAsync();

        vm.ActiveSession.Should().NotBeNull();
        vm.ActiveSession!.SessionTitle.Should().Be("Signed in later");
        orchestratorMock.Verify(o => o.LoadActiveSessionAsync(), Times.Exactly(2));
        orchestratorMock.Verify(
            o => o.BeginLocalSessionTracking(session, session.TotalElapsedSeconds),
            Times.Once);
    }
}
