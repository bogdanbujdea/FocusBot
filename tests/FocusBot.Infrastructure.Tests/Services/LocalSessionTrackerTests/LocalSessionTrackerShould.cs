using FocusBot.Core.Entities;
using FocusBot.Infrastructure.Services;

namespace FocusBot.Infrastructure.Tests.Services.LocalSessionTrackerTests;

public class LocalSessionTrackerShould
{
    private static AlignmentResult Aligned(int score = 8) => new() { Score = score, Reason = "On-task" };
    private static AlignmentResult Distracted(int score = 2) => new() { Score = score, Reason = "Off-task" };

    [Fact]
    public void IncrementFocusedSeconds_WhenTickAfterAlignedClassification()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordClassification("chrome", Aligned());
        tracker.RecordTick();

        // Assert
        var summary = tracker.GetSessionSummary();
        summary.FocusedSeconds.Should().Be(1);
        summary.DistractedSeconds.Should().Be(0);
    }

    [Fact]
    public void IncrementDistractedSeconds_WhenTickAfterDistractedClassification()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordClassification("youtube", Distracted());
        tracker.RecordTick();

        // Assert
        var summary = tracker.GetSessionSummary();
        summary.DistractedSeconds.Should().Be(1);
        summary.FocusedSeconds.Should().Be(0);
    }

    [Fact]
    public void SkipAccounting_WhenIdle()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");
        tracker.RecordClassification("vscode", Aligned());
        tracker.HandleIdle(true);

        // Act
        tracker.RecordTick();

        // Assert
        var summary = tracker.GetSessionSummary();
        summary.FocusedSeconds.Should().Be(0);
        summary.DistractedSeconds.Should().Be(0);
    }

    [Fact]
    public void ResumeAccounting_WhenIdleCleared()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");
        tracker.RecordClassification("vscode", Aligned());
        tracker.HandleIdle(true);
        tracker.RecordTick(); // skipped

        // Act
        tracker.HandleIdle(false);
        tracker.RecordTick();

        // Assert
        tracker.GetSessionSummary().FocusedSeconds.Should().Be(1);
    }

    [Fact]
    public void ReturnOneHundredPercent_WhenPurelyFocused()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");
        tracker.RecordClassification("vscode", Aligned());

        // Act
        for (var i = 0; i < 10; i++)
            tracker.RecordTick();

        // Assert
        tracker.GetFocusScore().Should().Be(100);
    }

    [Fact]
    public void ReturnZeroPercent_WhenPurelyDistracted()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");
        tracker.RecordClassification("youtube", Distracted());

        // Act
        for (var i = 0; i < 10; i++)
            tracker.RecordTick();

        // Assert
        tracker.GetFocusScore().Should().Be(0);
    }

    [Fact]
    public void ReturnFiftyPercent_WhenHalfFocusedHalfDistracted()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordClassification("vscode", Aligned());
        for (var i = 0; i < 5; i++)
            tracker.RecordTick();
        tracker.RecordClassification("youtube", Distracted());
        for (var i = 0; i < 5; i++)
            tracker.RecordTick();

        // Assert
        tracker.GetFocusScore().Should().Be(50);
    }

    [Fact]
    public void IncrementDistractionCount_OnAlignedToDistractedTransition()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordClassification("vscode", Aligned());   // aligned
        tracker.RecordClassification("youtube", Distracted()); // distraction starts

        // Assert
        tracker.GetSessionSummary().DistractionCount.Should().Be(1);
    }

    [Fact]
    public void NotIncrementDistractionCount_WhenStaysDistracted()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordClassification("youtube", Distracted());
        tracker.RecordClassification("youtube", Distracted()); // still distracted

        // Assert
        tracker.GetSessionSummary().DistractionCount.Should().Be(0);
    }

    [Fact]
    public void IncrementContextSwitchCount_WhenProcessChanges()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordClassification("vscode", Aligned());
        tracker.RecordClassification("chrome", Aligned()); // context switch

        // Assert
        tracker.GetSessionSummary().ContextSwitchCount.Should().Be(1);
    }

    [Fact]
    public void ResetAllCounters_WhenResetCalled()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");
        tracker.RecordClassification("youtube", Distracted());
        tracker.RecordTick();
        tracker.RecordClassification("chrome", Aligned());
        tracker.RecordTick();

        // Act
        tracker.Reset();

        // Assert
        var summary = tracker.GetSessionSummary();
        summary.FocusedSeconds.Should().Be(0);
        summary.DistractedSeconds.Should().Be(0);
        summary.DistractionCount.Should().Be(0);
        summary.ContextSwitchCount.Should().Be(0);
    }

    [Fact]
    public void TrackTopAlignedApp_InSessionSummary()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordClassification("vscode", Aligned());
        tracker.RecordTick();
        tracker.RecordTick();

        // Assert
        var summary = tracker.GetSessionSummary();
        summary.TopAlignedApps.Should().Contain("vscode");
    }

    [Fact]
    public void NotCountTime_WhenNoClassificationRecorded()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordTick();
        tracker.RecordTick();
        tracker.RecordTick();

        // Assert
        var summary = tracker.GetSessionSummary();
        summary.FocusedSeconds.Should().Be(0);
        summary.DistractedSeconds.Should().Be(0);
    }

    [Fact]
    public void AccumulateTimePerTick_UsingLastClassificationState()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");
        tracker.RecordClassification("vscode", Aligned());

        // Act — simulate 5 seconds passing on the same window without new classification
        for (var i = 0; i < 5; i++)
            tracker.RecordTick();

        // Assert
        var summary = tracker.GetSessionSummary();
        summary.FocusedSeconds.Should().Be(5);
        summary.DistractedSeconds.Should().Be(0);
    }

    [Fact]
    public void GetFocusedSeconds_MatchesSessionSummary()
    {
        var tracker = new LocalSessionTracker();
        tracker.Start("task");
        tracker.RecordClassification("vscode", Aligned());
        tracker.RecordTick();
        tracker.RecordTick();

        tracker.GetFocusedSeconds().Should().Be(2);
        tracker.GetSessionSummary().FocusedSeconds.Should().Be(2);
    }

    [Fact]
    public void GetDistractedSeconds_MatchesSessionSummary()
    {
        var tracker = new LocalSessionTracker();
        tracker.Start("task");
        tracker.RecordClassification("youtube", Distracted());
        tracker.RecordTick();

        tracker.GetDistractedSeconds().Should().Be(1);
        tracker.GetSessionSummary().DistractedSeconds.Should().Be(1);
    }

    [Fact]
    public void GetDistractionCount_MatchesSessionSummary()
    {
        var tracker = new LocalSessionTracker();
        tracker.Start("task");
        tracker.RecordClassification("vscode", Aligned());
        tracker.RecordClassification("youtube", Distracted());

        tracker.GetDistractionCount().Should().Be(1);
        tracker.GetSessionSummary().DistractionCount.Should().Be(1);
    }
}
