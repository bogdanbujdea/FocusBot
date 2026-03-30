using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.CurrentWindowStatusBarViewModelTests;

public class UpdateFromOrchestratorShould
{
    [Fact]
    public void UpdateAllProperties_WhenStateChanges()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        var stateArgs = new FocusSessionStateChangedEventArgs
        {
            SessionElapsedSeconds = 60,
            FocusScorePercent = 80,
            IsClassifying = false,
            FocusScore = 8,
            FocusReason = "Working on task code",
            HasCurrentFocusResult = true,
            IsSessionPaused = false,
            CurrentProcessName = "devenv",
            CurrentWindowTitle = "Solution.sln",
        };

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, stateArgs);

        // Assert
        vm.FocusScore.Should().Be(8);
        vm.FocusReason.Should().Be("Working on task code");
        vm.HasCurrentFocusResult.Should().BeTrue();
        vm.CurrentProcessName.Should().Be("devenv");
        vm.CurrentWindowTitle.Should().Be("Solution.sln");
    }
}
