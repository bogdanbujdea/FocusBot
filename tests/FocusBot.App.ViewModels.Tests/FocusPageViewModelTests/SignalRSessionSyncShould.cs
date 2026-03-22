using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class SignalRSessionSyncShould
{
    [Fact]
    public async Task SessionStarted_SameSessionId_DoesNotReloadFromApi()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var id = Guid.NewGuid();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(id, "Local task", null, null, DateTime.UtcNow, null));

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync(session);

        var hub = new FakeFocusHubClient();
        var vm = CreateVm(orchestratorMock, hub);

        await Task.Delay(200);

        hub.RaiseSessionStarted(
            new SessionStartedEvent(id, "Local task", null, DateTime.UtcNow, "api"));

        await Task.Delay(200);

        orchestratorMock.Verify(o => o.LoadActiveSessionAsync(), Times.Once);
    }

    [Fact]
    public async Task SessionStarted_NewSessionId_ReloadsFromApi()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstSession = UserSession.FromApiResponse(
            new ApiSessionResponse(firstId, "First", null, null, DateTime.UtcNow, null));
        var secondSession = UserSession.FromApiResponse(
            new ApiSessionResponse(secondId, "Second", null, null, DateTime.UtcNow, null));

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock
            .SetupSequence(o => o.LoadActiveSessionAsync())
            .ReturnsAsync(firstSession)
            .ReturnsAsync(secondSession);

        var hub = new FakeFocusHubClient();
        var vm = CreateVm(orchestratorMock, hub);

        await Task.Delay(200);
        vm.ActiveSession!.SessionId.Should().Be(firstId.ToString());

        hub.RaiseSessionStarted(
            new SessionStartedEvent(secondId, "Second", null, DateTime.UtcNow, "api"));

        await Task.Delay(300);

        orchestratorMock.Verify(o => o.LoadActiveSessionAsync(), Times.AtLeast(2));
        vm.ActiveSession!.SessionId.Should().Be(secondId.ToString());
        orchestratorMock.Verify(
            o => o.BeginLocalSessionTracking(secondSession, secondSession.TotalElapsedSeconds),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SessionEnded_TriggersBoardReload()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync((UserSession?)null);

        var hub = new FakeFocusHubClient();
        _ = CreateVm(orchestratorMock, hub);

        await Task.Delay(200);

        hub.RaiseSessionEnded(new SessionEndedEvent(Guid.NewGuid(), DateTime.UtcNow, "api"));

        await Task.Delay(300);

        orchestratorMock.Verify(o => o.LoadActiveSessionAsync(), Times.AtLeast(2));
        orchestratorMock.Verify(o => o.StopLocalTrackingIfActive(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SessionPaused_ForCurrentSession_CallsOrchestratorPause()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var id = Guid.NewGuid();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(id, "Task", null, null, DateTime.UtcNow, null));

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync(session);

        var hub = new FakeFocusHubClient();
        _ = CreateVm(orchestratorMock, hub);

        await Task.Delay(200);

        hub.RaiseSessionPaused(new SessionPausedEvent(id, DateTime.UtcNow, "api"));

        await Task.Delay(100);

        orchestratorMock.Verify(o => o.PauseSession(), Times.Once);
    }

    [Fact]
    public async Task SessionResumed_ForCurrentSession_CallsOrchestratorResume()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var id = Guid.NewGuid();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(id, "Task", null, null, DateTime.UtcNow, null));

        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync(session);

        var hub = new FakeFocusHubClient();
        _ = CreateVm(orchestratorMock, hub);

        await Task.Delay(200);

        hub.RaiseSessionResumed(new SessionResumedEvent(id, "api"));

        await Task.Delay(100);

        orchestratorMock.Verify(o => o.ResumeSession(), Times.Once);
    }

    private static FocusPageViewModel CreateVm(Mock<IFocusSessionOrchestrator> orchestratorMock, IFocusHubClient hub)
    {
        var navMock = new Mock<INavigationService>();
        var settingsMock = new Mock<ISettingsService>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var statusBar = new FocusStatusViewModel(orchestratorMock.Object);
        return new FocusPageViewModel(
            navMock.Object,
            settingsMock.Object,
            orchestratorMock.Object,
            hub,
            accountVm,
            statusBar);
    }
}
