using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Devices;
using FocusBot.WebAPI.Features.Sessions;

namespace FocusBot.WebAPI.IntegrationTests;

public class SessionTests(CustomWebApplicationFactory factory)
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
    public async Task SessionEndpoints_Return401_WithoutAuth()
    {
        var client = factory.CreateClient();
        var sessionId = Guid.NewGuid();
        var endRequest = CreateEndRequest(DeviceId: null);

        var postResponse = await client.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("Test task", null, DeviceId: null)
        );
        var endResponse = await client.PostAsJsonAsync($"/sessions/{sessionId}/end", endRequest);
        var activeResponse = await client.GetAsync("/sessions/active");
        var listResponse = await client.GetAsync("/sessions?page=1&pageSize=10");
        var byIdResponse = await client.GetAsync($"/sessions/{sessionId}");

        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        endResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        activeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        listResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        byIdResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullSessionLifecycle_WithAuth_ReturnsExpectedResponses()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        var meResponse = await client.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var startResponse = await client.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("Integration test task", "Some hints", DeviceId: null)
        );
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var startedSession = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        startedSession.Should().NotBeNull();
        startedSession!.SessionTitle.Should().Be("Integration test task");
        startedSession.SessionContext.Should().Be("Some hints");
        startedSession.EndedAtUtc.Should().BeNull();

        var activeResponse = await client.GetAsync("/sessions/active");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activeSession = await activeResponse.Content.ReadFromJsonAsync<SessionResponse>();
        activeSession.Should().NotBeNull();
        activeSession!.Id.Should().Be(startedSession.Id);

        var conflictResponse = await client.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("Second task", null, DeviceId: null)
        );
        var conflictBody = await conflictResponse.Content.ReadAsStringAsync();

        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        conflictBody.Should().Contain("An active session already exists.");

        var endResponse = await client.PostAsJsonAsync(
            $"/sessions/{startedSession.Id}/end",
            CreateEndRequest()
        );
        endResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var endedSession = await endResponse.Content.ReadFromJsonAsync<SessionResponse>();
        endedSession.Should().NotBeNull();
        endedSession!.FocusScorePercent.Should().Be(90);
        endedSession.EndedAtUtc.Should().NotBeNull();

        var noActiveResponse = await client.GetAsync("/sessions/active");
        noActiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await noActiveResponse.Content.ReadFromJsonAsync<SessionResponse>()).Should().BeNull();

        var historyResponse = await client.GetAsync("/sessions?page=1&pageSize=10");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await historyResponse.Content.ReadFromJsonAsync<
            PaginatedResponse<SessionResponse>
        >();
        history.Should().NotBeNull();
        history!.TotalCount.Should().Be(1);
        history.Items.Should().ContainSingle(s => s.Id == startedSession.Id);

        var byIdResponse = await client.GetAsync($"/sessions/{startedSession.Id}");
        byIdResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var byIdSession = await byIdResponse.Content.ReadFromJsonAsync<SessionResponse>();
        byIdSession.Should().NotBeNull();
        byIdSession!.Id.Should().Be(startedSession.Id);
    }

    [Fact]
    public async Task EndSession_Returns404_WithError_WhenSessionNotFound()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        var meResponse = await client.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsJsonAsync(
            $"/sessions/{Guid.NewGuid()}/end",
            CreateEndRequest()
        );
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("Session not found.");
    }

    [Fact]
    public async Task EndSession_Returns409_WithError_WhenSessionAlreadyEnded()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        var meResponse = await client.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var startResponse = await client.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("Task", null, DeviceId: null)
        );
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();

        var firstEndResponse = await client.PostAsJsonAsync(
            $"/sessions/{session!.Id}/end",
            CreateEndRequest()
        );
        firstEndResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondEndResponse = await client.PostAsJsonAsync(
            $"/sessions/{session.Id}/end",
            CreateEndRequest()
        );
        var secondEndBody = await secondEndResponse.Content.ReadAsStringAsync();

        secondEndResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        secondEndBody.Should().Contain("Session is already ended.");
    }

    [Fact]
    public async Task SessionEndpoints_AreScopedByOwner_ForReadAndEnd()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var ownerClient = CreateAuthenticatedClient(ownerId);
        var otherClient = CreateAuthenticatedClient(otherUserId);

        await ownerClient.GetAsync("/auth/me");
        await otherClient.GetAsync("/auth/me");

        var startResponse = await ownerClient.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("Owner session", null, DeviceId: null)
        );
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();

        var getByIdAsOther = await otherClient.GetAsync($"/sessions/{session!.Id}");
        getByIdAsOther.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var endAsOther = await otherClient.PostAsJsonAsync(
            $"/sessions/{session.Id}/end",
            CreateEndRequest()
        );
        var endAsOtherBody = await endAsOther.Content.ReadAsStringAsync();
        endAsOther.StatusCode.Should().Be(HttpStatusCode.NotFound);
        endAsOtherBody.Should().Contain("Session not found.");
    }

    [Fact]
    public async Task EndSession_Returns403_WithError_WhenDeviceBelongsToDifferentUser()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var ownerClient = CreateAuthenticatedClient(ownerId);
        var otherClient = CreateAuthenticatedClient(otherUserId);

        await ownerClient.GetAsync("/auth/me");
        await otherClient.GetAsync("/auth/me");

        var otherDeviceResponse = await otherClient.PostAsJsonAsync(
            "/devices",
            new RegisterDeviceRequest(
                DeviceType.Desktop,
                Name: "Other user's device",
                Fingerprint: "other-user-device-fp",
                AppVersion: "1.0.0",
                Platform: "windows"
            )
        );
        otherDeviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var otherDevice = await otherDeviceResponse.Content.ReadFromJsonAsync<DeviceResponse>();
        otherDevice.Should().NotBeNull();

        var startResponse = await ownerClient.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("Owner session", null, DeviceId: null)
        );
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();

        var endResponse = await ownerClient.PostAsJsonAsync(
            $"/sessions/{session!.Id}/end",
            CreateEndRequest(otherDevice!.Id)
        );
        var endBody = await endResponse.Content.ReadAsStringAsync();

        endResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        endBody.Should().Contain("Device does not belong to the current user.");

        var stillActiveResponse = await ownerClient.GetAsync("/sessions/active");
        stillActiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EndSession_Returns200_AndAssociatesOwnedDevice_WhenDeviceBelongsToCurrentUser()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        var registerResponse = await client.PostAsJsonAsync(
            "/devices",
            new RegisterDeviceRequest(
                DeviceType.Desktop,
                Name: "Owner desktop",
                Fingerprint: "owner-desktop-fp",
                AppVersion: "1.0.0",
                Platform: "windows"
            )
        );
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var device = await registerResponse.Content.ReadFromJsonAsync<DeviceResponse>();
        device.Should().NotBeNull();

        var startResponse = await client.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("Task with device", null, DeviceId: null)
        );
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();

        var endResponse = await client.PostAsJsonAsync(
            $"/sessions/{session!.Id}/end",
            CreateEndRequest(device!.Id)
        );

        endResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var endedSession = await endResponse.Content.ReadFromJsonAsync<SessionResponse>();
        endedSession.Should().NotBeNull();
        endedSession!.DeviceId.Should().Be(device.Id);
    }

    [Fact]
    public async Task PauseAndResumeSession_UpdatesStateCorrectly()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        // Start a session
        var startResponse = await client.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("Pauseable task", null, DeviceId: null)
        );
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();
        session!.IsPaused.Should().BeFalse();
        session.PausedAtUtc.Should().BeNull();
        session.TotalPausedSeconds.Should().Be(0);

        // Pause the session
        var pauseResponse = await client.PostAsJsonAsync($"/sessions/{session.Id}/pause", new { });
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pausedSession = await pauseResponse.Content.ReadFromJsonAsync<SessionResponse>();
        pausedSession.Should().NotBeNull();
        pausedSession!.IsPaused.Should().BeTrue();
        pausedSession.PausedAtUtc.Should().NotBeNull();
        pausedSession.EndedAtUtc.Should().BeNull();

        // Verify active endpoint returns paused session
        var activeResponse = await client.GetAsync("/sessions/active");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activeSession = await activeResponse.Content.ReadFromJsonAsync<SessionResponse>();
        activeSession.Should().NotBeNull();
        activeSession!.IsPaused.Should().BeTrue();

        // Wait a bit to accumulate pause time
        await Task.Delay(1100);

        // Resume the session
        var resumeResponse = await client.PostAsJsonAsync(
            $"/sessions/{session.Id}/resume",
            new { }
        );
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resumedSession = await resumeResponse.Content.ReadFromJsonAsync<SessionResponse>();
        resumedSession.Should().NotBeNull();
        resumedSession!.IsPaused.Should().BeFalse();
        resumedSession.PausedAtUtc.Should().BeNull();
        resumedSession.TotalPausedSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PauseEndpoints_Return404_WhenSessionNotFound()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        var nonExistentId = Guid.NewGuid();

        var pauseResponse = await client.PostAsJsonAsync(
            $"/sessions/{nonExistentId}/pause",
            new { }
        );
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var resumeResponse = await client.PostAsJsonAsync(
            $"/sessions/{nonExistentId}/resume",
            new { }
        );
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PauseEndpoints_Return409_WithInvalidStateTransitions()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        // Start a session
        var startResponse = await client.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("State transition test", null, DeviceId: null)
        );
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();

        // Try to resume a running session (should fail)
        var resumeRunningResponse = await client.PostAsJsonAsync(
            $"/sessions/{session!.Id}/resume",
            new { }
        );
        var resumeRunningBody = await resumeRunningResponse.Content.ReadAsStringAsync();
        resumeRunningResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        resumeRunningBody.Should().Contain("not paused");

        // Pause the session
        var pauseResponse = await client.PostAsJsonAsync($"/sessions/{session.Id}/pause", new { });
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Try to pause a paused session (should fail)
        var pausePausedResponse = await client.PostAsJsonAsync(
            $"/sessions/{session.Id}/pause",
            new { }
        );
        var pausePausedBody = await pausePausedResponse.Content.ReadAsStringAsync();
        pausePausedResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        pausePausedBody.Should().Contain("already paused");
    }

    [Fact]
    public async Task EndPausedSession_AccumulatesPauseDuration()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        // Start a session
        var startResponse = await client.PostAsJsonAsync(
            "/sessions",
            new StartSessionRequest("Pause then end", null, DeviceId: null)
        );
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();

        // Pause the session
        var pauseResponse = await client.PostAsJsonAsync($"/sessions/{session!.Id}/pause", new { });
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait to accumulate pause time
        await Task.Delay(1100);

        // End the paused session
        var endResponse = await client.PostAsJsonAsync(
            $"/sessions/{session.Id}/end",
            CreateEndRequest()
        );
        endResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var endedSession = await endResponse.Content.ReadFromJsonAsync<SessionResponse>();
        endedSession.Should().NotBeNull();
        endedSession!.EndedAtUtc.Should().NotBeNull();
        endedSession.PausedAtUtc.Should().BeNull();
        endedSession.TotalPausedSeconds.Should().BeGreaterThan(0);
        endedSession.IsPaused.Should().BeFalse(); // Ended sessions are not paused
    }

    private static EndSessionRequest CreateEndRequest(Guid? DeviceId = null) =>
        new(
            FocusScorePercent: 90,
            FocusedSeconds: 1800,
            DistractedSeconds: 200,
            DistractionCount: 3,
            ContextSwitchCount: 60,
            DeviceId: DeviceId
        );
}
