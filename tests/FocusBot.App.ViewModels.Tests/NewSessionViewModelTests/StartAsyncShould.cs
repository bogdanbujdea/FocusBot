using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using Moq;

namespace FocusBot.App.ViewModels.Tests.NewSessionViewModelTests;

public class StartAsyncShould
{
    private static ApiSessionResponse CreateTestSession() => new(
        Id: Guid.NewGuid(),
        SessionTitle: "Test Task",
        SessionContext: "Test context",
        StartedAtUtc: DateTime.UtcNow,
        EndedAtUtc: null,
        PausedAtUtc: null,
        TotalPausedSeconds: 0,
        IsPaused: false,
        Source: "desktop"
    );

    [Fact]
    public async Task InvokeApiClient()
    {
        // Arrange
        var mockApi = new Mock<IFocusBotApiClient>();
        mockApi.Setup(x => x.StartSessionAsync(It.IsAny<StartSessionPayload>()))
            .ReturnsAsync(ApiResult<ApiSessionResponse>.Success(CreateTestSession()));

        var vm = new NewSessionViewModel(mockApi.Object)
        {
            SessionTitle = "Test Task",
            SessionContext = "Test context"
        };

        // Act
        await vm.StartCommand.ExecuteAsync(null);

        // Assert
        mockApi.Verify(x => x.StartSessionAsync(
            It.Is<StartSessionPayload>(p =>
                p.SessionTitle == "Test Task" &&
                p.SessionContext == "Test context")),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUI_When_ResultIsSuccessful()
    {
        // Arrange
        var testSession = CreateTestSession();
        var mockApi = new Mock<IFocusBotApiClient>();
        mockApi.Setup(x => x.StartSessionAsync(It.IsAny<StartSessionPayload>()))
            .ReturnsAsync(ApiResult<ApiSessionResponse>.Success(testSession));

        var vm = new NewSessionViewModel(mockApi.Object)
        {
            SessionTitle = "Test Task",
            SessionContext = "Test context"
        };

        ApiSessionResponse? callbackSession = null;
        vm.OnSessionStarted += session => callbackSession = session;

        // Act
        await vm.StartCommand.ExecuteAsync(null);

        // Assert
        callbackSession.Should().NotBeNull();
        callbackSession!.Id.Should().Be(testSession.Id);
        vm.SessionTitle.Should().BeEmpty();
        vm.SessionContext.Should().BeEmpty();
        vm.State.IsBusy.Should().BeFalse();
        vm.State.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ShowError_When_ErrorReturned()
    {
        // Arrange
        var mockApi = new Mock<IFocusBotApiClient>();
        mockApi.Setup(x => x.StartSessionAsync(It.IsAny<StartSessionPayload>()))
            .ReturnsAsync(ApiResult<ApiSessionResponse>.Failure(System.Net.HttpStatusCode.InternalServerError));

        var vm = new NewSessionViewModel(mockApi.Object)
        {
            SessionTitle = "Test Task"
        };

        ApiSessionResponse? callbackSession = null;
        vm.OnSessionStarted += session => callbackSession = session;

        // Act
        await vm.StartCommand.ExecuteAsync(null);

        // Assert
        callbackSession.Should().BeNull();
        vm.State.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.State.IsBusy.Should().BeFalse();
        vm.SessionTitle.Should().Be("Test Task");
    }

    [Fact]
    public async Task ClearError_When_ClearErrorCommandExecuted()
    {
        // Arrange
        var mockApi = new Mock<IFocusBotApiClient>();
        mockApi.Setup(x => x.StartSessionAsync(It.IsAny<StartSessionPayload>()))
            .ReturnsAsync(ApiResult<ApiSessionResponse>.Failure(System.Net.HttpStatusCode.InternalServerError));

        var vm = new NewSessionViewModel(mockApi.Object)
        {
            SessionTitle = "Test Task"
        };

        await vm.StartCommand.ExecuteAsync(null);
        vm.State.ErrorMessage.Should().NotBeNullOrEmpty();

        // Act
        vm.ClearErrorCommand.Execute(null);

        // Assert
        vm.State.ErrorMessage.Should().BeNull();
        vm.State.IsBusy.Should().BeFalse();
    }
}
