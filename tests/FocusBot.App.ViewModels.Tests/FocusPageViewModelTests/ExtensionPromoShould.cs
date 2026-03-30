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
        var (vm, orchestratorMock) = CreateViewModel(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "msedge", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeTrue();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeTrue_When_ProcessName_Is_Chrome()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModel(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "chrome", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeTrue();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeTrue_When_ProcessName_Is_Microsoft_Edge()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModel(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "Microsoft Edge", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeTrue();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeFalse_When_ProcessName_Is_Firefox()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModel(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "firefox", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeFalse();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeFalse_When_ProcessName_Is_NonBrowser()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModel(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "devenv", "MyFile.cs");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeFalse();
    }

    [Fact]
    public async Task ShowExtensionPromo_BeTrue_When_Foreground_Is_Edge()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModel(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "msedge", "Tab");

        vm.ShowExtensionPromo.Should().BeTrue();
    }

    [Fact]
    public async Task ShowExtensionPromo_BeFalse_When_Foreground_Is_Firefox()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, orchestratorMock) = CreateViewModel(ctx);
        RaiseOrchestratorStateChanged(orchestratorMock, "firefox", "Tab");

        vm.ShowExtensionPromo.Should().BeFalse();
    }

    [Fact]
    public async Task ExtensionStoreUris_Be_Valid_Absolute_Uris()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, _) = CreateViewModel(ctx);

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

    private static (FocusPageViewModel vm, Mock<IFocusSessionOrchestrator> orchestratorMock) CreateViewModel(FocusPageTestContext _)
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
            statusBar);
        return (vm, orchestratorMock);
    }
}
