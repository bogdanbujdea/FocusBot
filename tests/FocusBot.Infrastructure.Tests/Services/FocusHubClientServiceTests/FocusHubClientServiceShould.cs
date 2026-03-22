using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services.FocusHubClientServiceTests;

public class FocusHubClientServiceShould
{
    [Fact]
    public void Instance_implements_IFocusHubClient_and_IAsyncDisposable()
    {
        var auth = new Mock<IAuthService>();
        var logger = Mock.Of<ILogger<FocusHubClientService>>();
        IFocusHubClient sut = new FocusHubClientService(auth.Object, logger, "http://localhost:5251");

        sut.Should().NotBeNull();
        sut.Should().BeAssignableTo<IAsyncDisposable>();
    }
}
