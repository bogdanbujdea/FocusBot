using CSharpFunctionalExtensions;
using FocusBot.Core.Entities;
using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services;

public class ForegroundClassificationCoordinatorTests
{
    private readonly Mock<IWindowMonitorService> _windowMonitor;
    private readonly Mock<IClassificationService> _classificationService;
    private readonly Mock<IExtensionPresenceService> _presenceService;
    private readonly ForegroundClassificationCoordinator _coordinator;

    public ForegroundClassificationCoordinatorTests()
    {
        _windowMonitor = new Mock<IWindowMonitorService>();
        _classificationService = new Mock<IClassificationService>();
        _presenceService = new Mock<IExtensionPresenceService>();

        _classificationService
            .Setup(x => x.ClassifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AlignmentResult { Score = 8, Reason = "Test reason" }));

        _coordinator = new ForegroundClassificationCoordinator(
            _windowMonitor.Object,
            _classificationService.Object,
            _presenceService.Object,
            Mock.Of<ILogger<ForegroundClassificationCoordinator>>());
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("msedge")]
    [InlineData("brave")]
    [InlineData("Chrome")]
    [InlineData("MSEDGE")]
    public async Task OnForegroundWindowChanged_SkipsClassification_WhenChromiumBrowserAndExtensionOnline(
        string processName)
    {
        // Arrange
        _presenceService.Setup(x => x.IsExtensionOnline).Returns(true);
        _coordinator.Start("Test task", "Test context");

        // Act
        RaiseForegroundWindowChanged(processName, "Test Window - Google Chrome");
        await Task.Delay(50);

        // Assert
        _classificationService.Verify(
            x => x.ClassifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("msedge")]
    [InlineData("brave")]
    public async Task OnForegroundWindowChanged_ClassifiesBrowser_WhenExtensionOffline(string processName)
    {
        // Arrange
        _presenceService.Setup(x => x.IsExtensionOnline).Returns(false);
        _coordinator.Start("Test task", "Test context");

        // Act
        RaiseForegroundWindowChanged(processName, "Test Window - Browser");
        await Task.Delay(50);

        // Assert
        _classificationService.Verify(
            x => x.ClassifyAsync(
                processName,
                "Test Window - Browser",
                "Test task",
                "Test context",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("firefox")]
    [InlineData("opera")]
    [InlineData("vivaldi")]
    public async Task OnForegroundWindowChanged_ClassifiesNonChromiumBrowser_WhenExtensionOnline(
        string processName)
    {
        // Arrange
        _presenceService.Setup(x => x.IsExtensionOnline).Returns(true);
        _coordinator.Start("Test task", "Test context");

        // Act
        RaiseForegroundWindowChanged(processName, "Test Window - Browser");
        await Task.Delay(50);

        // Assert
        _classificationService.Verify(
            x => x.ClassifyAsync(
                processName,
                "Test Window - Browser",
                "Test task",
                "Test context",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Code", "Program.cs - Visual Studio Code")]
    [InlineData("slack", "general - Slack")]
    [InlineData("notepad", "Untitled - Notepad")]
    [InlineData("devenv", "Solution Explorer - Visual Studio")]
    public async Task OnForegroundWindowChanged_ClassifiesNonBrowserApp_WhenExtensionOnline(
        string processName,
        string windowTitle)
    {
        // Arrange
        _presenceService.Setup(x => x.IsExtensionOnline).Returns(true);
        _coordinator.Start("Test task", "Test context");

        // Act
        RaiseForegroundWindowChanged(processName, windowTitle);
        await Task.Delay(50);

        // Assert
        _classificationService.Verify(
            x => x.ClassifyAsync(
                processName,
                windowTitle,
                "Test task",
                "Test context",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnForegroundWindowChanged_SkipsClassification_WhenEmptyProcessAndTitle()
    {
        // Arrange
        _presenceService.Setup(x => x.IsExtensionOnline).Returns(false);
        _coordinator.Start("Test task", "Test context");

        // Act
        RaiseForegroundWindowChanged("", "");
        await Task.Delay(50);

        // Assert
        _classificationService.Verify(
            x => x.ClassifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Foqus", "Foqus")]
    [InlineData("foqus", "Active Session")]
    [InlineData("Taskmgr", "Task Manager")]
    [InlineData("TASKMGR", "Task Manager")]
    [InlineData("explorer", "")]
    [InlineData("Explorer", "File Explorer")]
    public async Task OnForegroundWindowChanged_SkipsClassification_ForExcludedProcesses(
        string processName,
        string windowTitle)
    {
        // Arrange
        _presenceService.Setup(x => x.IsExtensionOnline).Returns(false);
        _coordinator.Start("Test task", "Test context");

        // Act
        RaiseForegroundWindowChanged(processName, windowTitle);
        await Task.Delay(50);

        // Assert
        _classificationService.Verify(
            x => x.ClassifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Stop_UnsubscribesFromWindowMonitor()
    {
        // Arrange
        _coordinator.Start("Test task", "Test context");

        // Act
        _coordinator.Stop();
        RaiseForegroundWindowChanged("notepad", "Untitled - Notepad");

        // Assert
        _classificationService.Verify(
            x => x.ClassifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void RaiseForegroundWindowChanged(string processName, string windowTitle)
    {
        _windowMonitor.Raise(
            x => x.ForegroundWindowChanged += null,
            this,
            new ForegroundWindowChangedEventArgs
            {
                ProcessName = processName,
                WindowTitle = windowTitle
            });
    }
}
