using CSharpFunctionalExtensions;
using FocusBot.Core.Entities;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FocusBot.Infrastructure.Tests.Services.DeviceServiceTests;

public class DeviceServiceShould
{
    private static readonly Guid SampleDeviceId = Guid.NewGuid();

    private static IDeviceService BuildService(
        Mock<IFocusBotApiClient> apiClient,
        Mock<ISettingsService> settings)
    {
        return new DesktopDeviceService(
            apiClient.Object,
            settings.Object,
            NullLogger<DesktopDeviceService>.Instance);
    }

    private static Mock<ISettingsService> SettingsWithNoStoredDevice()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingAsync<string>("Device_Id")).ReturnsAsync((string?)null);
        settings.Setup(s => s.GetSettingAsync<string>("Device_Fingerprint")).ReturnsAsync("fixed-fingerprint");
        settings.Setup(s => s.GetSettingAsync<string>("Device_Name")).ReturnsAsync((string?)null);
        return settings;
    }

    private static Mock<ISettingsService> SettingsWithStoredDevice(Guid deviceId)
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingAsync<string>("Device_Id")).ReturnsAsync(deviceId.ToString());
        settings.Setup(s => s.GetSettingAsync<string>("Device_Fingerprint")).ReturnsAsync("fixed-fingerprint");
        return settings;
    }

    [Fact]
    public async Task ReturnFailure_WhenNotAuthenticated()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(false);
        var settings = SettingsWithNoStoredDevice();
        var sut = BuildService(apiClient, settings);

        // Act
        var result = await sut.RegisterAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterDevice_WhenAuthenticated()
    {
        // Arrange
        var response = new ApiDeviceResponse(
            SampleDeviceId, "desktop", "My PC", "fixed-fingerprint", DateTime.UtcNow);

        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.Setup(a => a.RegisterDeviceAsync(It.IsAny<string>(), It.IsAny<string>()))
                 .ReturnsAsync(response);

        var settings = SettingsWithNoStoredDevice();
        var sut = BuildService(apiClient, settings);

        // Act
        var result = await sut.RegisterAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.GetDeviceId().Should().Be(SampleDeviceId);
    }

    [Fact]
    public async Task SkipHeartbeat_WhenNoDeviceId()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        var settings = SettingsWithNoStoredDevice();
        var sut = BuildService(apiClient, settings);

        // Act
        await sut.SendHeartbeatAsync();

        // Assert
        apiClient.Verify(a => a.SendHeartbeatAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SendHeartbeat_WhenDeviceIdExists()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.Setup(a => a.SendHeartbeatAsync(SampleDeviceId)).ReturnsAsync(true);

        var settings = SettingsWithStoredDevice(SampleDeviceId);
        var sut = BuildService(apiClient, settings);

        // Act
        await sut.SendHeartbeatAsync();

        // Assert
        apiClient.Verify(a => a.SendHeartbeatAsync(SampleDeviceId), Times.Once);
    }

    [Fact]
    public async Task ClearDeviceId_WhenHeartbeatFails()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.Setup(a => a.SendHeartbeatAsync(SampleDeviceId)).ReturnsAsync(false);

        var settings = SettingsWithStoredDevice(SampleDeviceId);
        var sut = BuildService(apiClient, settings);

        // Act — heartbeat fails
        await sut.SendHeartbeatAsync();

        // Assert — cached device ID was cleared so the next heartbeat will skip
        sut.GetDeviceId().Should().BeNull();
    }

    [Fact]
    public async Task Deregister_WhenDeviceIdExists()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        apiClient.Setup(a => a.IsConfigured).Returns(true);
        apiClient.Setup(a => a.DeregisterDeviceAsync(SampleDeviceId)).ReturnsAsync(true);

        var settings = SettingsWithStoredDevice(SampleDeviceId);
        settings.Setup(s => s.SetSettingAsync<string?>("Device_Id", null)).Returns(Task.CompletedTask);
        var sut = BuildService(apiClient, settings);

        // Act
        await sut.DeregisterAsync();

        // Assert
        apiClient.Verify(a => a.DeregisterDeviceAsync(SampleDeviceId), Times.Once);
        sut.GetDeviceId().Should().BeNull();
    }

    [Fact]
    public async Task SkipDeregister_WhenNoDeviceId()
    {
        // Arrange
        var apiClient = new Mock<IFocusBotApiClient>();
        var settings = SettingsWithNoStoredDevice();
        var sut = BuildService(apiClient, settings);

        // Act
        await sut.DeregisterAsync();

        // Assert
        apiClient.Verify(a => a.DeregisterDeviceAsync(It.IsAny<Guid>()), Times.Never);
    }
}
