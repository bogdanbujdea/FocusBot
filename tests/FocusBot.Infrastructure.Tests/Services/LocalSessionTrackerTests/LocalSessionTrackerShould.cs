using FocusBot.Core.Entities;
using FocusBot.Infrastructure.Services;

namespace FocusBot.Infrastructure.Tests.Services.LocalSessionTrackerTests;

public class LocalSessionTrackerShould
{
    private static AlignmentResult Aligned(int score = 8) => new() { Score = score, Reason = "On-task" };
    private static AlignmentResult Distracted(int score = 2) => new() { Score = score, Reason = "Off-task" };

    [Fact]
    public void IncrementFocusedSeconds_WhenRecordingAlignedClassification()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordClassification("chrome", Aligned());

        // Assert
        var summary = tracker.GetSessionSummary();
        summary.FocusedSeconds.Should().Be(1);
        summary.DistractedSeconds.Should().Be(0);
    }

    [Fact]
    public void IncrementDistractedSeconds_WhenRecordingDistractedClassification()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        tracker.RecordClassification("youtube", Distracted());

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
        tracker.HandleIdle(true);

        // Act
        tracker.RecordClassification("vscode", Aligned());

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
        tracker.HandleIdle(true);
        tracker.RecordClassification("vscode", Aligned()); // skipped

        // Act
        tracker.HandleIdle(false);
        tracker.RecordClassification("vscode", Aligned());

        // Assert
        tracker.GetSessionSummary().FocusedSeconds.Should().Be(1);
    }

    [Fact]
    public void ReturnOneHundredPercent_WhenPurelyFocused()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        for (var i = 0; i < 10; i++)
            tracker.RecordClassification("vscode", Aligned());

        // Assert
        tracker.GetFocusScore().Should().Be(100);
    }

    [Fact]
    public void ReturnZeroPercent_WhenPurelyDistracted()
    {
        // Arrange
        var tracker = new LocalSessionTracker();
        tracker.Start("task");

        // Act
        for (var i = 0; i < 10; i++)
            tracker.RecordClassification("youtube", Distracted());

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
        for (var i = 0; i < 5; i++)
            tracker.RecordClassification("vscode", Aligned());
        for (var i = 0; i < 5; i++)
            tracker.RecordClassification("youtube", Distracted());

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
        tracker.RecordClassification("chrome", Aligned());

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
        tracker.RecordClassification("vscode", Aligned());

        // Assert
        var summary = tracker.GetSessionSummary();
        summary.TopAlignedApps.Should().Contain("vscode");
    }
}
