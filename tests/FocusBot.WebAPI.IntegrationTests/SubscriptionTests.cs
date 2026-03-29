using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FocusBot.WebAPI.Data.Entities;
using FocusBot.WebAPI.Features.Auth;
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
    public async Task GetMe_CreatesTrialOnFirstSignIn()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        // GET /auth/me provisions user + trial in one shot
        var meResponse = await client.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        me.Should().NotBeNull();
        me!.SubscriptionStatus.Should().Be(SubscriptionStatus.Trial);
        me.PlanType.Should().Be(PlanType.TrialFullAccess);
    }

    [Fact]
    public async Task GetStatus_ReturnsTrial_AfterProvisioning()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        // Provision user + trial via /auth/me
        var meResponse = await client.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // /subscriptions/status reads the existing trial row
        var statusResponse = await client.GetAsync("/subscriptions/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await statusResponse.Content.ReadFromJsonAsync<SubscriptionStatusResponse>();
        status.Should().NotBeNull();
        status!.Status.Should().Be(SubscriptionStatus.Trial);
        status.PlanType.Should().Be(PlanType.TrialFullAccess);
        status.TrialEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GetStatus_IsIdempotent_ForExistingTrial()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        await client.GetAsync("/auth/me");

        // Multiple status calls — no duplicate rows created
        await client.GetAsync("/subscriptions/status");
        var secondResponse = await client.GetAsync("/subscriptions/status");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await secondResponse.Content.ReadFromJsonAsync<SubscriptionStatusResponse>();
        status!.Status.Should().Be(SubscriptionStatus.Trial);
    }

    [Fact]
    public async Task PostTrial_Returns409_WhenTrialAlreadyCreatedByProvisioning()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        // Provisioning via /auth/me creates the trial
        await client.GetAsync("/auth/me");

        // Explicit POST /trial should conflict because the row already exists
        var trialRequest = new StringContent(
            """{"planType":1}""",
            System.Text.Encoding.UTF8,
            "application/json"
        );
        var conflictResponse = await client.PostAsync("/subscriptions/trial", trialRequest);
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostTrial_ActivatesTrial_WithAuth()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        // Provision user + trial via /auth/me
        var meResponse = await client.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Explicit POST /trial returns 409 since provisioning already created the trial
        var trialRequest = new StringContent(
            """{"planType":1}""",
            System.Text.Encoding.UTF8,
            "application/json"
        );
        var trialResponse = await client.PostAsync("/subscriptions/trial", trialRequest);
        trialResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Status still reflects the trial created by provisioning
        var statusResponse = await client.GetAsync("/subscriptions/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await statusResponse.Content.ReadFromJsonAsync<SubscriptionStatusResponse>();
        status.Should().NotBeNull();
        status!.Status.Should().Be(SubscriptionStatus.Trial);
        status.TrialEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(10));
    }
}
