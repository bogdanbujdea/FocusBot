using FocusBot.Core.Events;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.KanbanBoardViewModelTests;

public class OnForegroundWindowChangedShould
{
    [Fact]
    public async Task SetProcessNameAndWindowTitle_When_Foreground_Changes()
    {
        // Arrange
        await using var ctx = await KanbanBoardTestContext.CreateAsync();
        var monitorMock = new Mock<IWindowMonitorService>();
        var vm = new KanbanBoardViewModel(ctx.Repo, monitorMock.Object);
        var eventArgs = new ForegroundWindowChangedEventArgs
        {
            ProcessName = "devenv",
            WindowTitle = "MyFile.cs",
        };

        // Act
        monitorMock.Raise(m => m.ForegroundWindowChanged += null, monitorMock.Object, eventArgs);

        // Assert
        vm.CurrentProcessName.Should().Be("devenv");
        vm.CurrentWindowTitle.Should().Be("MyFile.cs");
    }
}
