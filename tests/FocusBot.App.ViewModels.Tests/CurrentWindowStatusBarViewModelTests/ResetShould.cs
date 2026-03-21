using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.CurrentWindowStatusBarViewModelTests;

public class ResetShould
{
    [Fact]
    public void ClearAllDisplayState()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);
        vm.IsMonitoring = true;

        // Set some state via orchestrator event
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object,
            new Core.Events.FocusSessionStateChangedEventArgs
            {
                SessionElapsedSeconds = 120, FocusScorePercent = 90, IsClassifying = false,
                FocusScore = 9, FocusReason = "Deep work", HasCurrentFocusResult = true,
                IsSessionPaused = false, CurrentProcessName = "code", CurrentWindowTitle = "main.ts",
            });

        // Act
        vm.Reset();

        // Assert
        vm.CurrentProcessName.Should().BeEmpty();
        vm.CurrentWindowTitle.Should().BeEmpty();
        vm.FocusScore.Should().Be(0);
        vm.FocusReason.Should().BeEmpty();
        vm.HasCurrentFocusResult.Should().BeFalse();
        vm.IsMonitoring.Should().BeFalse();
        vm.FocusScoreCategory.Should().Be("Distracted");
        vm.ShowCheckingMessage.Should().BeFalse();
        vm.ShowMarkOverrideButton.Should().BeFalse();
    }
}
