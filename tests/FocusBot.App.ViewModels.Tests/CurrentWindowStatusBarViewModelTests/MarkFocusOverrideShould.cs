using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.CurrentWindowStatusBarViewModelTests;

public class MarkFocusOverrideShould
{
    [Fact]
    public async Task CallRecordManualOverride_WithDistracting_WhenCurrentlyFocused()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        orchestratorMock.Raise(
            m => m.StateChanged += null,
            orchestratorMock.Object,
            new FocusSessionStateChangedEventArgs
            {
                SessionElapsedSeconds = 0,
                FocusScorePercent = 80,
                IsClassifying = false,
                FocusScore = 8,
                FocusReason = "Working",
                HasCurrentFocusResult = true,
                IsSessionPaused = false,
                CurrentProcessName = "devenv",
                CurrentWindowTitle = "Code",
            }
        );

        // Act
        await vm.MarkFocusOverrideCommand.ExecuteAsync(null);

        // Assert
        orchestratorMock.Verify(
            o => o.RecordManualOverrideAsync(2, "Manually marked as Distracting"),
            Times.Once
        );
    }

    [Fact]
    public async Task CallRecordManualOverride_WithFocused_WhenCurrentlyDistracted()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        orchestratorMock.Raise(
            m => m.StateChanged += null,
            orchestratorMock.Object,
            new FocusSessionStateChangedEventArgs
            {
                SessionElapsedSeconds = 0,
                FocusScorePercent = 20,
                IsClassifying = false,
                FocusScore = 2,
                FocusReason = "Social media",
                HasCurrentFocusResult = true,
                IsSessionPaused = false,
                CurrentProcessName = "chrome",
                CurrentWindowTitle = "Twitter",
            }
        );

        // Act
        await vm.MarkFocusOverrideCommand.ExecuteAsync(null);

        // Assert
        orchestratorMock.Verify(
            o => o.RecordManualOverrideAsync(9, "Manually marked as Focused"),
            Times.Once
        );
    }
}
