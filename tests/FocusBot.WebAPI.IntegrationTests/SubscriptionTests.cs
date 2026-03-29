using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Subscriptions;

namespace FocusBot.WebAPI.IntegrationTests;

public class SubscriptionTests(CustomWebApplicationFactory factory)
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
    public async Task GetStatus_Returns401_WithoutAuth()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/subscriptions/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostTrial_ActivatesTrial_WithAuth()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        // Provision user first
        var meResponse = await client.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Activate trial
        var trialRequest = new StringContent(
            """{"planType":1}""",
            System.Text.Encoding.UTF8,
            "application/json"
        );
        var trialResponse = await client.PostAsync("/subscriptions/trial", trialRequest);
        trialResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var trial = await trialResponse.Content.ReadFromJsonAsync<ActivateTrialResponse>();
        trial.Should().NotBeNull();
        trial!.Status.Should().Be(SubscriptionStatus.Trial);
        trial.TrialEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(10));

        // Verify status reflects the trial
        var statusResponse = await client.GetAsync("/subscriptions/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await statusResponse.Content.ReadFromJsonAsync<SubscriptionStatusResponse>();
        status.Should().NotBeNull();
        status!.Status.Should().Be(SubscriptionStatus.Trial);

        // Second trial activation should conflict
        var conflictResponse = await client.PostAsync("/subscriptions/trial", trialRequest);
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
