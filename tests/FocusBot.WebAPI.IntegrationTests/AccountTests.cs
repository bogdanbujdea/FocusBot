using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FocusBot.WebAPI.Features.Sessions;

namespace FocusBot.WebAPI.IntegrationTests;

public class AccountTests(CustomWebApplicationFactory factory)
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
    public async Task DeleteAccount_Returns401_WithoutAuth()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/auth/account");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAccount_DeletesUserAndAllData()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        var startResponse = await client.PostAsJsonAsync("/sessions",
            new StartSessionRequest("Session to delete", null, ClientId: null));
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();
        await client.PostAsJsonAsync($"/sessions/{session!.Id}/end",
            new EndSessionRequest(80, 1800, 300, 5, 10, null));

        var deleteResponse = await client.DeleteAsync("/auth/account");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionsResponse = await client.GetAsync("/sessions?page=1&pageSize=10");
        sessionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await sessionsResponse.Content.ReadFromJsonAsync<PaginatedResponse<SessionResponse>>();
        sessions!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAccount_DoesNotAffectOtherUsers()
    {
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var client1 = CreateAuthenticatedClient(user1Id);
        var client2 = CreateAuthenticatedClient(user2Id);

        await client1.GetAsync("/auth/me");
        await client2.GetAsync("/auth/me");

        var start2 = await client2.PostAsJsonAsync("/sessions",
            new StartSessionRequest("User2 session", null, ClientId: null));
        var session2 = await start2.Content.ReadFromJsonAsync<SessionResponse>();
        await client2.PostAsJsonAsync($"/sessions/{session2!.Id}/end",
            new EndSessionRequest(90, 2400, 200, 3, 8, null));

        await client1.DeleteAsync("/auth/account");

        var user2Sessions = await client2.GetAsync("/sessions?page=1&pageSize=10");
        var user2Data = await user2Sessions.Content.ReadFromJsonAsync<PaginatedResponse<SessionResponse>>();
        user2Data!.TotalCount.Should().Be(1);
    }
}
