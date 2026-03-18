using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    public async Task PostSessions_Returns401_WithoutAuth()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/sessions",
            new StartSessionRequest("Test task", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullSessionLifecycle_WithAuth()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        // Provision user first
        var meResponse = await client.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Start a session
        var startResponse = await client.PostAsJsonAsync("/sessions",
            new StartSessionRequest("Integration test task", "Some hints"));
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        session.Should().NotBeNull();
        session!.TaskText.Should().Be("Integration test task");
        session.EndedAtUtc.Should().BeNull();

        // Verify active session exists
        var activeResponse = await client.GetAsync("/sessions/active");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Starting another session should conflict
        var conflictResponse = await client.PostAsJsonAsync("/sessions",
            new StartSessionRequest("Second task", null));
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // End the session
        var endRequest = new EndSessionRequest(90, 1800, 200, 3, 60, null);
        var endResponse = await client.PostAsJsonAsync($"/sessions/{session.Id}/end", endRequest);
        endResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var endedSession = await endResponse.Content.ReadFromJsonAsync<SessionResponse>();
        endedSession!.FocusScorePercent.Should().Be(90);
        endedSession.EndedAtUtc.Should().NotBeNull();

        // Active session should no longer exist
        var noActiveResponse = await client.GetAsync("/sessions/active");
        noActiveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Session should appear in history
        var historyResponse = await client.GetAsync("/sessions?page=1&pageSize=10");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Get session by ID
        var byIdResponse = await client.GetAsync($"/sessions/{session.Id}");
        byIdResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
