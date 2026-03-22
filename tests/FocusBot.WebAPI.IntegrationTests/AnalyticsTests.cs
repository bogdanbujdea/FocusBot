using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FocusBot.WebAPI.Features.Analytics;
using FocusBot.WebAPI.Features.Sessions;

namespace FocusBot.WebAPI.IntegrationTests;

public class AnalyticsTests(CustomWebApplicationFactory factory)
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
    public async Task AnalyticsEndpoints_Return401_WithoutAuth()
    {
        var client = factory.CreateClient();

        var summaryResponse = await client.GetAsync("/analytics/summary");
        var trendsResponse = await client.GetAsync("/analytics/trends");
        var devicesResponse = await client.GetAsync("/analytics/devices");

        summaryResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        trendsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        devicesResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSummary_ReturnsZeros_WhenNoSessions()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        var response = await client.GetAsync("/analytics/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<AnalyticsSummaryResponse>();
        summary.Should().NotBeNull();
        summary!.TotalSessions.Should().Be(0);
        summary.TotalFocusedSeconds.Should().Be(0);
    }

    [Fact]
    public async Task GetSummary_ReturnsAggregatedData_AfterCompletedSession()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        var startResponse = await client.PostAsJsonAsync("/sessions",
            new StartSessionRequest("Analytics test", null, DeviceId: null));
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();

        var endResponse = await client.PostAsJsonAsync($"/sessions/{session!.Id}/end",
            new EndSessionRequest(85, 1800, 300, 5, 10, null));
        endResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaryResponse = await client.GetAsync("/analytics/summary");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await summaryResponse.Content.ReadFromJsonAsync<AnalyticsSummaryResponse>();
        summary.Should().NotBeNull();
        summary!.TotalSessions.Should().Be(1);
        summary.TotalFocusedSeconds.Should().Be(1800);
        summary.TotalDistractedSeconds.Should().Be(300);
        summary.AverageFocusScorePercent.Should().Be(85);
    }

    [Fact]
    public async Task GetTrends_ReturnsDataPoints()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        var startResponse = await client.PostAsJsonAsync("/sessions",
            new StartSessionRequest("Trend test", null, DeviceId: null));
        var session = await startResponse.Content.ReadFromJsonAsync<SessionResponse>();

        await client.PostAsJsonAsync($"/sessions/{session!.Id}/end",
            new EndSessionRequest(75, 2400, 600, 8, 15, null));

        var trendsResponse = await client.GetAsync("/analytics/trends?granularity=daily");
        trendsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var trends = await trendsResponse.Content.ReadFromJsonAsync<AnalyticsTrendsResponse>();
        trends.Should().NotBeNull();
        trends!.Granularity.Should().Be("daily");
        trends.DataPoints.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetDevices_ReturnsEmptyList_WhenNoDeviceSessions()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        var devicesResponse = await client.GetAsync("/analytics/devices");
        devicesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var devices = await devicesResponse.Content.ReadFromJsonAsync<AnalyticsDevicesResponse>();
        devices.Should().NotBeNull();
        devices!.Devices.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyticsData_IsScopedToUser()
    {
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var client1 = CreateAuthenticatedClient(userId1);
        var client2 = CreateAuthenticatedClient(userId2);

        await client1.GetAsync("/auth/me");
        await client2.GetAsync("/auth/me");

        var start1 = await client1.PostAsJsonAsync("/sessions",
            new StartSessionRequest("User1 session", null, DeviceId: null));
        var session1 = await start1.Content.ReadFromJsonAsync<SessionResponse>();
        await client1.PostAsJsonAsync($"/sessions/{session1!.Id}/end",
            new EndSessionRequest(90, 3600, 100, 1, 5, null));

        var summary2 = await client2.GetAsync("/analytics/summary");
        var data = await summary2.Content.ReadFromJsonAsync<AnalyticsSummaryResponse>();
        data!.TotalSessions.Should().Be(0);
    }
}
