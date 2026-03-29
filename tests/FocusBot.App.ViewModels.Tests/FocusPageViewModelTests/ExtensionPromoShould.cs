using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

public class ExtensionPromoShould
{
    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeTrue_When_ProcessName_Is_Msedge()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "msedge", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeTrue();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeTrue_When_ProcessName_Is_Chrome()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "chrome", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeTrue();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeTrue_When_ProcessName_Is_Microsoft_Edge()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "Microsoft Edge", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeTrue();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeFalse_When_ProcessName_Is_Firefox()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "firefox", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeFalse();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeFalse_When_ProcessName_Is_NonBrowser()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "devenv", "MyFile.cs");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeFalse();
    }

    [Fact]
    public async Task ShowExtensionPromo_BeTrue_When_ExtensionNotConnected_And_Foreground_Is_Edge()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var integrationMock = new Mock<IIntegrationService>();
        integrationMock.Setup(x => x.IsExtensionConnected).Returns(false);
        var (vm, orchestratorMock) = CreateViewModelWithIntegration(ctx, integrationMock.Object);
        RaiseOrchestratorStateChanged(orchestratorMock, "msedge", "Tab");

        vm.ShowExtensionPromo.Should().BeTrue();
    }

    [Fact]
    public async Task ShowExtensionPromo_BeFalse_When_ExtensionConnected()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var integrationMock = new Mock<IIntegrationService>();
        integrationMock.Setup(x => x.IsExtensionConnected).Returns(true);
        var (vm, orchestratorMock) = CreateViewModelWithIntegration(ctx, integrationMock.Object);
        RaiseOrchestratorStateChanged(orchestratorMock, "msedge", "Tab");
        integrationMock.Raise(m => m.ExtensionConnectionChanged += null, integrationMock.Object, true);

        vm.ShowExtensionPromo.Should().BeFalse();
    }

    [Fact]
    public async Task ShowExtensionPromo_BeFalse_When_ExtensionNotConnected_But_Foreground_Is_Firefox()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var integrationMock = new Mock<IIntegrationService>();
        integrationMock.Setup(x => x.IsExtensionConnected).Returns(false);
        var (vm, orchestratorMock) = CreateViewModelWithIntegration(ctx, integrationMock.Object);
        RaiseOrchestratorStateChanged(orchestratorMock, "firefox", "Tab");

        vm.ShowExtensionPromo.Should().BeFalse();
    }

    [Fact]
    public async Task ExtensionStoreUris_Be_Valid_Absolute_Uris()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, _) = CreateViewModelWithoutIntegration(ctx);

        vm.ExtensionStoreEdgeUri.Should().NotBeNull();
        vm.ExtensionStoreEdgeUri.IsAbsoluteUri.Should().BeTrue();
        vm.ExtensionStoreChromeUri.Should().NotBeNull();
        vm.ExtensionStoreChromeUri.IsAbsoluteUri.Should().BeTrue();
    }

    private static void RaiseOrchestratorStateChanged(Mock<IFocusSessionOrchestrator> orchestratorMock, string processName, string windowTitle)
    {
        var stateArgs = new FocusSessionStateChangedEventArgs
        {
            SessionElapsedSeconds = 0,
            FocusScorePercent = 0,
            IsClassifying = false,
            FocusScore = 0,
            FocusReason = string.Empty,
            HasCurrentFocusResult = false,
            IsSessionPaused = false,
            CurrentProcessName = processName,
            CurrentWindowTitle = windowTitle,
        };
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, stateArgs);
    }

    private static (FocusPageViewModel vm, Mock<IFocusSessionOrchestrator> orchestratorMock) CreateViewModelWithoutIntegration(FocusPageTestContext _)
    {
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync((UserSession?)null);
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
            statusBar,
            integrationService: null,
            uiDispatcher: null);
        return (vm, orchestratorMock);
    }

    private static (FocusPageViewModel vm, Mock<IFocusSessionOrchestrator> orchestratorMock) CreateViewModelWithIntegration(
        FocusPageTestContext _,
        IIntegrationService integrationService)
    {
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        orchestratorMock.Setup(o => o.LoadActiveSessionAsync()).ReturnsAsync((UserSession?)null);
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
            statusBar,
            integrationService,
            uiDispatcher: null);
        return (vm, orchestratorMock);
    }
}
