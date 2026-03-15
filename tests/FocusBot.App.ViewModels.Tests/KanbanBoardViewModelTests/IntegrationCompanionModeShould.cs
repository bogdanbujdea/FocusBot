using FocusBot.Core.DTOs;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.KanbanBoardViewModelTests;

public class IntegrationCompanionModeShould
{
    [Fact]
    public async Task ShowRemoteTaskInDisplayInProgress_When_TaskStartedReceived_From_Extension()
    {
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var integrationMock = new Mock<IIntegrationService>();
        var vm = CreateViewModel(ctx, monitorMock, integrationMock.Object, uiDispatcher: null);

        var payload = new TaskStartedPayload
        {
            TaskId = "ext-session-1",
            TaskText = "Watch a movie",
            TaskHints = null
        };

        integrationMock.Raise(m => m.TaskStartedReceived += null, integrationMock.Object, payload);

        vm.DisplayInProgressTasks.Should().HaveCount(1);
        vm.DisplayInProgressTasks[0].TaskId.Should().Be("ext-session-1");
        vm.DisplayInProgressTasks[0].Description.Should().Be("Watch a movie");
    }

    [Fact]
    public async Task StartWindowMonitor_When_TaskStartedReceived_From_Extension()
    {
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var integrationMock = new Mock<IIntegrationService>();
        var vm = CreateViewModel(ctx, monitorMock, integrationMock.Object, uiDispatcher: null);

        var payload = new TaskStartedPayload
        {
            TaskId = "ext-1",
            TaskText = "Code review",
            TaskHints = null
        };

        integrationMock.Raise(m => m.TaskStartedReceived += null, integrationMock.Object, payload);

        monitorMock.Verify(m => m.Start(), Times.Once);
    }

    [Fact]
    public async Task StopWindowMonitor_When_TaskEndedReceived_And_No_Local_InProgress_Task()
    {
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var integrationMock = new Mock<IIntegrationService>();

        var vm = CreateViewModel(ctx, monitorMock, integrationMock.Object, uiDispatcher: null);

        var payload = new TaskStartedPayload { TaskId = "ext-1", TaskText = "Task", TaskHints = null };
        integrationMock.Raise(m => m.TaskStartedReceived += null, integrationMock.Object, payload);
        monitorMock.Reset();

        integrationMock.Raise(m => m.TaskEndedReceived += null, integrationMock.Object, EventArgs.Empty);

        monitorMock.Verify(m => m.Stop(), Times.Once);
    }

    [Fact]
    public async Task Not_StopWindowMonitor_When_TaskEndedReceived_And_Local_InProgress_Task_Exists()
    {
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var task = await ctx.Repo.AddTaskAsync("Local task");
        await ctx.Repo.SetStatusToAsync(task.TaskId, FocusBot.Core.Entities.TaskStatus.InProgress);

        var monitorMock = new Mock<IWindowMonitorService>();
        var integrationMock = new Mock<IIntegrationService>();

        var vm = CreateViewModel(ctx, monitorMock, integrationMock.Object, uiDispatcher: null);
        await Task.Delay(300);

        monitorMock.Reset();

        integrationMock.Raise(m => m.TaskEndedReceived += null, integrationMock.Object, EventArgs.Empty);

        monitorMock.Verify(m => m.Stop(), Times.Never);
    }

    private static KanbanBoardViewModel CreateViewModel(
        KanbanBoardTestContext ctx,
        Mock<IWindowMonitorService> monitorMock,
        IIntegrationService integrationService,
        IUIThreadDispatcher? uiDispatcher)
    {
        var navMock = new Mock<INavigationService>();
        var llmMock = new Mock<ILlmService>();
        var settingsMock = new Mock<ISettingsService>();
        var timeTrackingMock = new Mock<ITimeTrackingService>();
        var idleDetectionMock = new Mock<IIdleDetectionService>();
        var focusScoreMock = new Mock<IFocusScoreService>();
        var trialMock = new Mock<ITrialService>();
        var distractionMock = new Mock<IDistractionDetectorService>();
        var distractionRepoMock = new Mock<IDistractionEventRepository>();
        var dailyAnalyticsMock = new Mock<IDailyAnalyticsService>();
        var alignmentCacheMock = new Mock<IAlignmentCacheRepository>();

        return new KanbanBoardViewModel(
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
            distractionRepoMock.Object,
            dailyAnalyticsMock.Object,
            alignmentCacheMock.Object,
            integrationService,
            uiDispatcher);
    }
}
