using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services.ClientServiceTests;

public class ClientServiceShould
{
    private static readonly Guid SampleClientId = Guid.NewGuid();

    private static IClientService BuildService(
        Mock<IFocusBotApiClient> apiClient,
        Mock<ISettingsService> settings
    )
    {
        return new DesktopClientService(
            apiClient.Object,
            settings.Object,
            NullLogger<DesktopClientService>.Instance
        );
    }

    private static Mock<ISettingsService> SettingsWithNoStoredClient()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingAsync<string>("Client_Id")).ReturnsAsync((string?)null);
        settings
            .Setup(s => s.GetSettingAsync<string>("Client_Fingerprint"))
            .ReturnsAsync("fixed-fingerprint");
        settings.Setup(s => s.GetSettingAsync<string>("Client_Name")).ReturnsAsync((string?)null);
        return settings;
    }

    private static Mock<ISettingsService> SettingsWithStoredClient(Guid clientId)
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(s => s.GetSettingAsync<string>("Client_Id"))
            .ReturnsAsync(clientId.ToString());
        settings
            .Setup(s => s.GetSettingAsync<string>("Client_Fingerprint"))
            .ReturnsAsync("fixed-fingerprint");
        return settings;
    }

    [Fact]
    public async Task ReturnFailure_WhenNotAuthenticated()
    {
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(false);
        var settings = SettingsWithNoStoredClient();
        var sut = BuildService(apiClient, settings);

        var result = await sut.RegisterAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterClient_WhenAuthenticated()
    {
        var response = new ApiClientResponse(
            SampleClientId,
            1,
            1,
            "My PC",
            "fixed-fingerprint",
            null,
            "Windows",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            true
        );

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient
            .Setup(a =>
                a.RegisterClientAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    ClientType.Desktop,
                    ClientHost.Windows))
            .ReturnsAsync(response);

        var settings = SettingsWithNoStoredClient();
        var sut = BuildService(apiClient, settings);

        var result = await sut.RegisterAsync();

        result.IsSuccess.Should().BeTrue();
        sut.GetClientId().Should().Be(SampleClientId);
    }

    [Fact]
    public async Task Deregister_WhenClientIdExists()
    {
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.Setup(a => a.DeregisterClientAsync(SampleClientId)).ReturnsAsync(true);

        var settings = SettingsWithStoredClient(SampleClientId);
        settings
            .Setup(s => s.SetSettingAsync<string?>("Client_Id", null))
            .Returns(Task.CompletedTask);
        var sut = BuildService(apiClient, settings);

        await sut.DeregisterAsync();

        apiClient.Verify(a => a.DeregisterClientAsync(SampleClientId), Times.Once);
        sut.GetClientId().Should().BeNull();
    }

    [Fact]
    public async Task SkipDeregister_WhenNoClientId()
    {
        var apiClient = new Mock<IFocusBotApiClient>();
        var settings = SettingsWithNoStoredClient();
        var sut = BuildService(apiClient, settings);

        await sut.DeregisterAsync();

        apiClient.Verify(a => a.DeregisterClientAsync(It.IsAny<Guid>()), Times.Never);
    }
}
