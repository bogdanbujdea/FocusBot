using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.HistoryViewModelTests;

public class HistoryViewModelShould
{
    private static UserSession CreateDoneTask(
        string id,
        string description,
        DateTime createdAtUtc,
        int? focusScorePercent = 85,
        long focusedSeconds = 3600,
        long distractedSeconds = 300,
        int distractionCount = 2
    )
    {
        return new UserSession
        {
            SessionId = id,
            Description = description,
            IsCompleted = true,
            CreatedAt = createdAtUtc,
            FocusScorePercent = focusScorePercent,
            FocusedSeconds = focusedSeconds,
            DistractedSeconds = distractedSeconds,
            DistractionCount = distractionCount,
            TotalElapsedSeconds = focusedSeconds + distractedSeconds,
        };
    }

    [Fact]
    public async Task LoadCompletedTasks_WhenInitialized()
    {
        var now = DateTime.UtcNow;
        var tasks = new List<UserSession>
        {
            CreateDoneTask("1", "Task one", now),
            CreateDoneTask("2", "Task two", now.AddHours(-1)),
        };
        var repoMock = new Mock<ISessionRepository>();
        repoMock.Setup(r => r.GetDoneSessionsAsync()).ReturnsAsync(tasks);
        var navMock = new Mock<INavigationService>();
        var vm = new HistoryViewModel(repoMock.Object, navMock.Object);

        await vm.InitializeAsync();

        repoMock.Verify(r => r.GetDoneSessionsAsync(), Times.Once);
        vm.TotalTasks.Should().Be(2);
        vm.HasData.Should().BeTrue();
        vm.DailyStats.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GroupTasksByDay_WhenMultipleDays()
    {
        var tz = TimeZoneInfo.Local;
        var todayLocal = DateTime.Now.Date;
        var yesterdayLocal = todayLocal.AddDays(-1);
        var todayUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal.AddHours(10), tz);
        var yesterdayUtc = TimeZoneInfo.ConvertTimeToUtc(yesterdayLocal.AddHours(14), tz);

        var tasks = new List<UserSession>
        {
            CreateDoneTask("1", "Today task", todayUtc),
            CreateDoneTask("2", "Yesterday task", yesterdayUtc),
        };
        var repoMock = new Mock<ISessionRepository>();
        repoMock.Setup(r => r.GetDoneSessionsAsync()).ReturnsAsync(tasks);
        var navMock = new Mock<INavigationService>();
        var vm = new HistoryViewModel(repoMock.Object, navMock.Object);

        await vm.InitializeAsync();

        vm.SelectedRange = DateRange.All;
        vm.DailyStats.Should().HaveCount(2);
        vm.DailyStats[0].DateDisplay.Should().Be("Today");
        vm.DailyStats[0].TaskCount.Should().Be(1);
        vm.DailyStats[1].DateDisplay.Should().Be("Yesterday");
        vm.DailyStats[1].TaskCount.Should().Be(1);
    }

    [Fact]
    public async Task FilterByDateRange_Today()
    {
        var tz = TimeZoneInfo.Local;
        var todayLocal = DateTime.Now.Date;
        var oldLocal = todayLocal.AddDays(-5);
        var todayUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal.AddHours(12), tz);
        var oldUtc = TimeZoneInfo.ConvertTimeToUtc(oldLocal.AddHours(12), tz);

        var tasks = new List<UserSession>
        {
            CreateDoneTask("1", "Today", todayUtc),
            CreateDoneTask("2", "Old", oldUtc),
        };
        var repoMock = new Mock<ISessionRepository>();
        repoMock.Setup(r => r.GetDoneSessionsAsync()).ReturnsAsync(tasks);
        var navMock = new Mock<INavigationService>();
        var vm = new HistoryViewModel(repoMock.Object, navMock.Object);

        await vm.InitializeAsync();
        vm.SelectedRange = DateRange.Today;

        vm.TotalTasks.Should().Be(1);
        vm.DailyStats.Should().ContainSingle();
        vm.DailyStats[0].DateDisplay.Should().Be("Today");
    }

    [Fact]
    public async Task FilterByDateRange_Week()
    {
        var tz = TimeZoneInfo.Local;
        var todayLocal = DateTime.Now.Date;
        var threeDaysAgo = todayLocal.AddDays(-3);
        var tenDaysAgo = todayLocal.AddDays(-10);
        var tasks = new List<UserSession>
        {
            CreateDoneTask(
                "1",
                "In range",
                TimeZoneInfo.ConvertTimeToUtc(threeDaysAgo.AddHours(12), tz)
            ),
            CreateDoneTask(
                "2",
                "Out of range",
                TimeZoneInfo.ConvertTimeToUtc(tenDaysAgo.AddHours(12), tz)
            ),
        };
        var repoMock = new Mock<ISessionRepository>();
        repoMock.Setup(r => r.GetDoneSessionsAsync()).ReturnsAsync(tasks);
        var navMock = new Mock<INavigationService>();
        var vm = new HistoryViewModel(repoMock.Object, navMock.Object);

        await vm.InitializeAsync();
        vm.SelectedRange = DateRange.Week;

        vm.TotalTasks.Should().Be(1);
    }

    [Fact]
    public async Task FilterByDateRange_Month()
    {
        var tz = TimeZoneInfo.Local;
        var todayLocal = DateTime.Now.Date;
        var twentyDaysAgo = todayLocal.AddDays(-20);
        var fortyDaysAgo = todayLocal.AddDays(-40);
        var tasks = new List<UserSession>
        {
            CreateDoneTask(
                "1",
                "In range",
                TimeZoneInfo.ConvertTimeToUtc(twentyDaysAgo.AddHours(12), tz)
            ),
            CreateDoneTask(
                "2",
                "Out of range",
                TimeZoneInfo.ConvertTimeToUtc(fortyDaysAgo.AddHours(12), tz)
            ),
        };
        var repoMock = new Mock<ISessionRepository>();
        repoMock.Setup(r => r.GetDoneSessionsAsync()).ReturnsAsync(tasks);
        var navMock = new Mock<INavigationService>();
        var vm = new HistoryViewModel(repoMock.Object, navMock.Object);

        await vm.InitializeAsync();
        vm.SelectedRange = DateRange.Month;

        vm.TotalTasks.Should().Be(1);
    }

    [Fact]
    public async Task CalculateAggregates_Correctly()
    {
        var now = DateTime.UtcNow;
        var tasks = new List<UserSession>
        {
            CreateDoneTask(
                "1",
                "A",
                now,
                focusScorePercent: 80,
                focusedSeconds: 3600,
                distractedSeconds: 600,
                distractionCount: 3
            ),
            CreateDoneTask(
                "2",
                "B",
                now.AddMinutes(-30),
                focusScorePercent: 100,
                focusedSeconds: 1800,
                distractedSeconds: 0,
                distractionCount: 0
            ),
        };
        var repoMock = new Mock<ISessionRepository>();
        repoMock.Setup(r => r.GetDoneSessionsAsync()).ReturnsAsync(tasks);
        var navMock = new Mock<INavigationService>();
        var vm = new HistoryViewModel(repoMock.Object, navMock.Object);

        await vm.InitializeAsync();

        vm.TotalTasks.Should().Be(2);
        vm.TotalFocusedSeconds.Should().Be(3600 + 1800);
        vm.TotalDistractedSeconds.Should().Be(600);
        vm.TotalDistractions.Should().Be(3);
        vm.AverageFocusScore.Should().Be(90);
    }

    [Fact]
    public void NavigateBack_WhenBackCommandExecuted()
    {
        var repoMock = new Mock<ISessionRepository>();
        repoMock.Setup(r => r.GetDoneSessionsAsync()).ReturnsAsync(new List<UserSession>());
        var navMock = new Mock<INavigationService>();
        var vm = new HistoryViewModel(repoMock.Object, navMock.Object);

        vm.BackCommand.Execute(null);

        navMock.Verify(n => n.NavigateToBoard(), Times.Once);
    }

    [Fact]
    public async Task ShowEmptyState_WhenNoTasksInRange()
    {
        var repoMock = new Mock<ISessionRepository>();
        repoMock.Setup(r => r.GetDoneSessionsAsync()).ReturnsAsync(new List<UserSession>());
        var navMock = new Mock<INavigationService>();
        var vm = new HistoryViewModel(repoMock.Object, navMock.Object);

        await vm.InitializeAsync();

        vm.HasData.Should().BeFalse();
        vm.ShowEmptyState.Should().BeTrue();
        vm.TotalTasks.Should().Be(0);
    }
}
