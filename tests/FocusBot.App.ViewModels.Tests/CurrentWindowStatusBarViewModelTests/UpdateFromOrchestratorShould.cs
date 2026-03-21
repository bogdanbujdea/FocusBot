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
        vm.IsClassifying.Should().BeFalse();
        vm.CurrentProcessName.Should().Be("devenv");
        vm.CurrentWindowTitle.Should().Be("Solution.sln");
    }

    [Fact]
    public void SetFocusScoreCategory_ToFocused_WhenScoreIsHighEnough()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateArgs(focusScore: 8));

        // Assert
        vm.FocusScoreCategory.Should().Be("Focused");
    }

    [Fact]
    public void SetFocusScoreCategory_ToUnclear_WhenScoreIsMedium()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateArgs(focusScore: 5));

        // Assert
        vm.FocusScoreCategory.Should().Be("Unclear");
    }

    [Fact]
    public void SetFocusScoreCategory_ToDistracted_WhenScoreIsLow()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateArgs(focusScore: 2));

        // Assert
        vm.FocusScoreCategory.Should().Be("Distracted");
    }

    [Fact]
    public void SetMarkOverrideButtonText_ToMarkAsDistracting_WhenFocused()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateArgs(focusScore: 8));

        // Assert
        vm.MarkOverrideButtonText.Should().Be("Mark as distracting");
    }

    [Fact]
    public void SetMarkOverrideButtonText_ToMarkAsFocused_WhenDistracted()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object, CreateArgs(focusScore: 2));

        // Assert
        vm.MarkOverrideButtonText.Should().Be("Mark as focused");
    }

    [Fact]
    public void ShowCheckingMessage_WhenMonitoring_AndNoResult()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);
        vm.IsMonitoring = true;

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object,
            CreateArgs(hasCurrentFocusResult: false));

        // Assert
        vm.ShowCheckingMessage.Should().BeTrue();
    }

    [Fact]
    public void HideCheckingMessage_WhenResultExists()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);
        vm.IsMonitoring = true;

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object,
            CreateArgs(hasCurrentFocusResult: true, focusScore: 7));

        // Assert
        vm.ShowCheckingMessage.Should().BeFalse();
    }

    [Fact]
    public void ShowMarkOverrideButton_WhenResultExists_AndNotClassifying_AndNotNeutral()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object,
            CreateArgs(hasCurrentFocusResult: true, isClassifying: false, focusScore: 8, focusReason: "Working on code"));

        // Assert
        vm.ShowMarkOverrideButton.Should().BeTrue();
    }

    [Fact]
    public void HideMarkOverrideButton_WhenClassifying()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object,
            CreateArgs(hasCurrentFocusResult: true, isClassifying: true, focusScore: 8));

        // Assert
        vm.ShowMarkOverrideButton.Should().BeFalse();
    }

    [Fact]
    public void HideMarkOverrideButton_WhenNeutralApp()
    {
        // Arrange
        var orchestratorMock = new Mock<IFocusSessionOrchestrator>();
        var vm = new FocusStatusViewModel(orchestratorMock.Object);

        // Act
        orchestratorMock.Raise(m => m.StateChanged += null, orchestratorMock.Object,
            CreateArgs(hasCurrentFocusResult: true, isClassifying: false, focusScore: 5, focusReason: "Neutral app"));

        // Assert
        vm.ShowMarkOverrideButton.Should().BeFalse();
    }

    private static FocusSessionStateChangedEventArgs CreateArgs(
        int focusScore = 0,
        string focusReason = "",
        bool hasCurrentFocusResult = false,
        bool isClassifying = false,
        string currentProcessName = "",
        string currentWindowTitle = "")
    {
        return new FocusSessionStateChangedEventArgs
        {
            SessionElapsedSeconds = 0,
            FocusScorePercent = 0,
            IsClassifying = isClassifying,
            FocusScore = focusScore,
            FocusReason = focusReason,
            HasCurrentFocusResult = hasCurrentFocusResult,
            IsSessionPaused = false,
            CurrentProcessName = currentProcessName,
            CurrentWindowTitle = currentWindowTitle,
        };
    }
}
