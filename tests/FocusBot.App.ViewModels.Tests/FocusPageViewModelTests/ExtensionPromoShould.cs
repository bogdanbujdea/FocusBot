using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class ExtensionPromoShould
{
    [Fact]
    public async Task IsExtensionConnected_BeFalse_When_ExtensionOffline()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(Guid.NewGuid(), "Task", null, null, DateTime.UtcNow, null)
        );
        var extensionPresence = new TestExtensionPresenceService(false);
        var hub = new FakeFocusHubClient();
        var vm = CreateViewModel(session, hub, extensionPresence);

        await Task.Delay(200);

        vm.IsExtensionConnected.Should().BeFalse();
    }

    [Fact]
    public async Task IsExtensionConnected_BeTrue_When_ExtensionOnline()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(Guid.NewGuid(), "Task", null, null, DateTime.UtcNow, null)
        );
        var extensionPresence = new TestExtensionPresenceService(true);
        var hub = new FakeFocusHubClient();
        var vm = CreateViewModel(session, hub, extensionPresence);

        await Task.Delay(200);

        vm.IsExtensionConnected.Should().BeTrue();
    }

    [Fact]
    public async Task IsExtensionConnected_BeTrue_WithoutSession_When_ExtensionOnline()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var extensionPresence = new TestExtensionPresenceService(true);
        var hub = new FakeFocusHubClient();
        var vm = CreateViewModel(null, hub, extensionPresence);

        await Task.Delay(200);

        vm.IsExtensionConnected.Should().BeTrue();
    }

    [Fact]
    public async Task ClassificationReasonText_BeShown_When_SessionIsActive_AndHubEventArrives()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(Guid.NewGuid(), "Task", null, null, DateTime.UtcNow, null)
        );
        var extensionPresence = new TestExtensionPresenceService(false);
        var hub = new FakeFocusHubClient();
        var (vm, orchestratorMock) = CreateViewModelWithOrchestrator(session, hub, extensionPresence);

        orchestratorMock
            .Setup(o => o.ApplyRemoteClassificationFromHub(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, int, string, string>((_, score, reason, _) =>
            {
                var args = new FocusSessionStateChangedEventArgs
                {
                    FocusScore = score,
                    FocusReason = reason,
                    HasCurrentFocusResult = true,
                    IsClassifying = false,
                    IsSessionPaused = false,
                    SessionElapsedSeconds = 0,
                    FocusScorePercent = 0,
                    CurrentProcessName = "msedge",
                    CurrentWindowTitle = "Example",
                };
                orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, args);
            });

        await Task.Delay(200);

        hub.RaiseClassificationChanged(
            new ClassificationChangedEvent(
                8,
                "SignalR says focused",
                "extension",
                "https://example.com",
                DateTime.UtcNow,
                false
            )
        );
        await Task.Delay(100);

        vm.ClassificationReasonText.Should().Be("SignalR says focused");
        vm.ShowClassificationMessage.Should().BeTrue();
        vm.ClassificationStatusLabel.Should().Be("Aligned");
        vm.IsClassificationAligned.Should().BeTrue();
    }

    [Fact]
    public async Task ShowClassificationMessage_BeFalse_When_NoSession()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var extensionPresence = new TestExtensionPresenceService(false);
        var hub = new FakeFocusHubClient();
        var vm = CreateViewModel(null, hub, extensionPresence);

        await Task.Delay(200);
        hub.RaiseClassificationChanged(
            new ClassificationChangedEvent(
                8,
                "SignalR says focused",
                "extension",
                "https://example.com",
                DateTime.UtcNow,
                false
            )
        );
        await Task.Delay(100);

        vm.ShowClassificationMessage.Should().BeFalse();
    }

    [Fact]
    public async Task ClassificationStatusLabel_Be_Analyzing_When_Classifying()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(Guid.NewGuid(), "Task", null, null, DateTime.UtcNow, null)
        );
        var (vm, orchestratorMock) = CreateViewModelWithOrchestrator(
            session,
            new FakeFocusHubClient(),
            new TestExtensionPresenceService(false)
        );

        await Task.Delay(200);
        RaiseOrchestratorStateChanged(orchestratorMock, isClassifying: true);

        vm.ClassificationStatusLabel.Should().Be("Analyzing page...");
        vm.IsClassificationNeutral.Should().BeTrue();
    }

    [Fact]
    public async Task ClassificationStatusLabel_Be_ClassifierError_When_AiErrorExists()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(Guid.NewGuid(), "Task", null, null, DateTime.UtcNow, null)
        );
        var (vm, orchestratorMock) = CreateViewModelWithOrchestrator(
            session,
            new FakeFocusHubClient(),
            new TestExtensionPresenceService(false)
        );

        await Task.Delay(200);
        RaiseOrchestratorStateChanged(orchestratorMock, aiRequestError: "OpenAI failed");

        vm.ClassificationStatusLabel.Should().Be("Classifier error");
        vm.ClassificationReasonText.Should().Be("OpenAI failed");
    }

    [Fact]
    public async Task ClassificationStatusLabel_Be_Waiting_When_NoResultYet()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(Guid.NewGuid(), "Task", null, null, DateTime.UtcNow, null)
        );
        var vm = CreateViewModel(
            session,
            new FakeFocusHubClient(),
            new TestExtensionPresenceService(false)
        );

        await Task.Delay(200);

        vm.ClassificationStatusLabel.Should().Be("Waiting for signal");
        vm.IsClassificationNeutral.Should().BeTrue();
    }

    [Fact]
    public async Task ClassificationStatusLabel_Be_Paused_When_SessionPaused()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var session = UserSession.FromApiResponse(
            new ApiSessionResponse(Guid.NewGuid(), "Task", null, null, DateTime.UtcNow, null)
        );
        var (vm, orchestratorMock) = CreateViewModelWithOrchestrator(
            session,
            new FakeFocusHubClient(),
            new TestExtensionPresenceService(false)
        );
        orchestratorMock.Setup(o => o.IsSessionPaused).Returns(true);

        await Task.Delay(200);
        RaiseOrchestratorStateChanged(orchestratorMock, hasCurrentFocusResult: true, focusScore: 9);

        vm.ClassificationStatusLabel.Should().Be("Paused");
        vm.ShowClassificationReason.Should().BeFalse();
    }

    private static FocusPageViewModel CreateViewModel(
        UserSession? activeSession,
        IFocusHubClient hub,
        IExtensionPresenceService extensionPresence
    )
    {
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync(activeSession);
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
            new StubPlanService(),
            accountVm,
            statusBar,
            extensionPresence: extensionPresence);
    }

    private static (FocusPageViewModel vm, Mock<IFocusSessionOrchestrator> orchestratorMock)
        CreateViewModelWithOrchestrator(
            UserSession? activeSession,
            IFocusHubClient hub,
            IExtensionPresenceService extensionPresence
        )
    {
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync(activeSession);
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
            hub,
            new StubPlanService(),
            accountVm,
            statusBar,
            extensionPresence: extensionPresence
        );
        return (vm, orchestratorMock);
    }

    private static void RaiseOrchestratorStateChanged(
        Mock<IFocusSessionOrchestrator> orchestratorMock,
        bool isClassifying = false,
        bool hasCurrentFocusResult = false,
        int focusScore = 0,
        string? aiRequestError = null
    )
    {
        var stateArgs = new FocusSessionStateChangedEventArgs
        {
            SessionElapsedSeconds = 0,
            FocusScorePercent = 0,
            IsClassifying = isClassifying,
            FocusScore = focusScore,
            FocusReason = string.Empty,
            HasCurrentFocusResult = hasCurrentFocusResult,
            IsSessionPaused = false,
            AiRequestError = aiRequestError,
            CurrentProcessName = "msedge",
            CurrentWindowTitle = "Some tab",
        };
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, stateArgs);
    }

    private sealed class TestExtensionPresenceService : IExtensionPresenceService
    {
        public TestExtensionPresenceService(bool isOnline) => IsExtensionOnline = isOnline;

        public bool IsExtensionOnline { get; private set; }
        public event EventHandler? ConnectionStateChanged;

        public void SetOnline(bool isOnline)
        {
            IsExtensionOnline = isOnline;
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task StartAsync() => Task.CompletedTask;
        public void Stop() { }
    }
}
