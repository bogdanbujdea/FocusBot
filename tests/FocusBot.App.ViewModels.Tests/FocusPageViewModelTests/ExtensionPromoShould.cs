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
        var (vm, monitorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseForegroundWindowChanged(monitorMock, "msedge", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeTrue();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeTrue_When_ProcessName_Is_Chrome()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, monitorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseForegroundWindowChanged(monitorMock, "chrome", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeTrue();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeTrue_When_ProcessName_Is_Microsoft_Edge()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, monitorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseForegroundWindowChanged(monitorMock, "Microsoft Edge", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeTrue();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeFalse_When_ProcessName_Is_Firefox()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, monitorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseForegroundWindowChanged(monitorMock, "firefox", "Some tab");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeFalse();
    }

    [Fact]
    public async Task IsForegroundBrowserEdgeOrChrome_BeFalse_When_ProcessName_Is_NonBrowser()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var (vm, monitorMock) = CreateViewModelWithoutIntegration(ctx);
        RaiseForegroundWindowChanged(monitorMock, "devenv", "MyFile.cs");

        vm.IsForegroundBrowserEdgeOrChrome.Should().BeFalse();
    }

    [Fact]
    public async Task ShowExtensionPromo_BeTrue_When_ExtensionNotConnected_And_Foreground_Is_Edge()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var integrationMock = new Mock<IIntegrationService>();
        integrationMock.Setup(x => x.IsExtensionConnected).Returns(false);
        var (vm, monitorMock) = CreateViewModelWithIntegration(ctx, integrationMock.Object);
        RaiseForegroundWindowChanged(monitorMock, "msedge", "Tab");

        vm.ShowExtensionPromo.Should().BeTrue();
    }

    [Fact]
    public async Task ShowExtensionPromo_BeFalse_When_ExtensionConnected()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var integrationMock = new Mock<IIntegrationService>();
        integrationMock.Setup(x => x.IsExtensionConnected).Returns(true);
        var (vm, monitorMock) = CreateViewModelWithIntegration(ctx, integrationMock.Object);
        RaiseForegroundWindowChanged(monitorMock, "msedge", "Tab");
        integrationMock.Raise(m => m.ExtensionConnectionChanged += null, integrationMock.Object, true);

        vm.ShowExtensionPromo.Should().BeFalse();
    }

    [Fact]
    public async Task ShowExtensionPromo_BeFalse_When_ExtensionNotConnected_But_Foreground_Is_Firefox()
    {
        await using var ctx = await FocusPageTestContext.CreateAsync();
        var integrationMock = new Mock<IIntegrationService>();
        integrationMock.Setup(x => x.IsExtensionConnected).Returns(false);
        var (vm, monitorMock) = CreateViewModelWithIntegration(ctx, integrationMock.Object);
        RaiseForegroundWindowChanged(monitorMock, "firefox", "Tab");

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

    private static void RaiseForegroundWindowChanged(Mock<IWindowMonitorService> monitorMock, string processName, string windowTitle)
    {
        var eventArgs = new ForegroundWindowChangedEventArgs
        {
            ProcessName = processName,
            WindowTitle = windowTitle,
        };
        monitorMock.Raise(m => m.ForegroundWindowChanged += null, monitorMock.Object, eventArgs);
    }

    private static (FocusPageViewModel vm, Mock<IWindowMonitorService> monitorMock) CreateViewModelWithoutIntegration(FocusPageTestContext ctx)
    {
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var trialMock = new Mock<ITrialService>();
        var distractionMock = new Mock<IDistractionDetectorService>();
        var dailyAnalyticsMock = new Mock<IDailyAnalyticsService>();
        var alignmentCacheMock = new Mock<IAlignmentCacheRepository>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            timeTrackingMock.Object,
            idleDetectionMock.Object,
            navMock.Object,
            llmMock.Object,
            settingsMock.Object,
            focusScoreMock.Object,
            trialMock.Object,
            distractionMock.Object,
            dailyAnalyticsMock.Object,
            alignmentCacheMock.Object,
            new Mock<ITaskSummaryService>().Object,
            accountVm,
            integrationService: null,
            uiDispatcher: null);
        return (vm, monitorMock);
    }

    private static (FocusPageViewModel vm, Mock<IWindowMonitorService> monitorMock) CreateViewModelWithIntegration(
        FocusPageTestContext ctx,
        IIntegrationService integrationService)
    {
        var monitorMock = new Mock<IWindowMonitorService>();
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var trialMock = new Mock<ITrialService>();
        var distractionMock = new Mock<IDistractionDetectorService>();
        var dailyAnalyticsMock = new Mock<IDailyAnalyticsService>();
        var alignmentCacheMock = new Mock<IAlignmentCacheRepository>();
        var accountVm = new AccountSettingsViewModel(
            Mock.Of<IAuthService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AccountSettingsViewModel>>());
        var vm = new FocusPageViewModel(
            ctx.Repo,
            monitorMock.Object,
            timeTrackingMock.Object,
            idleDetectionMock.Object,
            navMock.Object,
            llmMock.Object,
            settingsMock.Object,
            focusScoreMock.Object,
            trialMock.Object,
            distractionMock.Object,
            dailyAnalyticsMock.Object,
            alignmentCacheMock.Object,
            new Mock<ITaskSummaryService>().Object,
            accountVm,
            integrationService,
            uiDispatcher: null);
        return (vm, monitorMock);
    }
}
