using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Devices;

namespace FocusBot.WebAPI.IntegrationTests;

public class DevicesTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = factory.CreateClient();
        var token = TestJwtHelper.GenerateTestJwt(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task DevicesEndpoints_Return401_WithoutAuth()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/devices");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterDevice_Returns400_WhenNameMissing()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);
        await client.GetAsync("/auth/me");

        var request = new RegisterDeviceRequest(
            DeviceType.Desktop,
            Name: " ",
            Fingerprint: "fingerprint-1",
            AppVersion: "1.0.0",
            Platform: "windows");

        var response = await client.PostAsJsonAsync("/devices", request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Name is required.");
    }

    [Fact]
    public async Task RegisterDevice_Returns400_WhenFingerprintMissing()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);
        await client.GetAsync("/auth/me");

        var request = new RegisterDeviceRequest(
            DeviceType.Extension,
            Name: "Work Browser",
            Fingerprint: " ",
            AppVersion: "2.0.0",
            Platform: "edge");

        var response = await client.PostAsJsonAsync("/devices", request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Fingerprint is required.");
    }

    [Fact]
    public async Task DevicesLifecycle_ReturnsExpectedResponses_WhenOwnedByUser()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        var meResponse = await client.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var registerRequest = new RegisterDeviceRequest(
            DeviceType.Desktop,
            Name: "Bogdan-PC",
            Fingerprint: "desktop-fp-1",
            AppVersion: "1.0.0",
            Platform: "windows");

        var registerResponse = await client.PostAsJsonAsync("/devices", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await registerResponse.Content.ReadFromJsonAsync<DeviceResponse>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("Bogdan-PC");
        created.Fingerprint.Should().Be("desktop-fp-1");
        created.DeviceType.Should().Be(DeviceType.Desktop);

        var listResponse = await client.GetAsync("/devices");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var devices = await listResponse.Content.ReadFromJsonAsync<List<DeviceResponse>>();
        devices.Should().NotBeNull();
        devices!.Should().ContainSingle(d => d.Id == created.Id);

        var heartbeatRequest = new HeartbeatRequest(AppVersion: "1.0.1", Platform: "win11");
        var heartbeatResponse = await client.PutAsJsonAsync($"/devices/{created.Id}/heartbeat", heartbeatRequest);
        heartbeatResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var heartbeatDevice = await heartbeatResponse.Content.ReadFromJsonAsync<DeviceResponse>();
        heartbeatDevice.Should().NotBeNull();
        heartbeatDevice!.AppVersion.Should().Be("1.0.1");
        heartbeatDevice.Platform.Should().Be("win11");

        var deleteResponse = await client.DeleteAsync($"/devices/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterDeleteResponse = await client.GetAsync("/devices");
        listAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var devicesAfterDelete = await listAfterDeleteResponse.Content.ReadFromJsonAsync<List<DeviceResponse>>();
        devicesAfterDelete.Should().NotBeNull();
        devicesAfterDelete!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDevices_ReturnsOnlyDevicesForCurrentUser()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var ownerClient = CreateAuthenticatedClient(ownerId);
        var otherClient = CreateAuthenticatedClient(otherUserId);

        await ownerClient.GetAsync("/auth/me");
        await otherClient.GetAsync("/auth/me");

        var ownerRegisterResponse = await ownerClient.PostAsJsonAsync("/devices", new RegisterDeviceRequest(
            DeviceType.Desktop,
            Name: "Owner device",
            Fingerprint: "owner-fp",
            AppVersion: "1.0.0",
            Platform: "windows"));
        ownerRegisterResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var otherRegisterResponse = await otherClient.PostAsJsonAsync("/devices", new RegisterDeviceRequest(
            DeviceType.Extension,
            Name: "Other device",
            Fingerprint: "other-fp",
            AppVersion: "1.0.0",
            Platform: "chrome"));
        otherRegisterResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var ownerListResponse = await ownerClient.GetAsync("/devices");
        ownerListResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ownerDevices = await ownerListResponse.Content.ReadFromJsonAsync<List<DeviceResponse>>();
        ownerDevices.Should().NotBeNull();
        ownerDevices!.Should().ContainSingle();
        ownerDevices[0].Fingerprint.Should().Be("owner-fp");
    }

    [Fact]
    public async Task HeartbeatAndDelete_Return404_WhenDeviceBelongsToDifferentUser()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var ownerClient = CreateAuthenticatedClient(ownerId);
        var otherClient = CreateAuthenticatedClient(otherUserId);

        await ownerClient.GetAsync("/auth/me");
        await otherClient.GetAsync("/auth/me");

        var registerResponse = await ownerClient.PostAsJsonAsync("/devices", new RegisterDeviceRequest(
            DeviceType.Desktop,
            Name: "Owner laptop",
            Fingerprint: "owner-heartbeat-fp",
            AppVersion: "1.0.0",
            Platform: "windows"));

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await registerResponse.Content.ReadFromJsonAsync<DeviceResponse>();
        created.Should().NotBeNull();

        var heartbeatResponse = await otherClient.PutAsJsonAsync(
            $"/devices/{created!.Id}/heartbeat",
            new HeartbeatRequest(AppVersion: "9.9.9", Platform: "other-platform"));
        heartbeatResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteResponse = await otherClient.DeleteAsync($"/devices/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}