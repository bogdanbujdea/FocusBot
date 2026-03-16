using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FocusBot.WebAPI.Features.Subscriptions;
using Microsoft.IdentityModel.Tokens;

namespace FocusBot.WebAPI.IntegrationTests;

public class SubscriptionTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static string GenerateTestJwt(Guid userId, string email = "test@example.com")
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("email", email)
        };

        var token = new JwtSecurityToken(
            issuer: $"{CustomWebApplicationFactory.TestSupabaseUrl}/auth/v1",
            audience: "authenticated",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = factory.CreateClient();
        var token = GenerateTestJwt(userId);
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
        var trialResponse = await client.PostAsync("/subscriptions/trial", null);
        trialResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var trial = await trialResponse.Content.ReadFromJsonAsync<ActivateTrialResponse>();
        trial.Should().NotBeNull();
        trial!.Status.Should().Be("trial");
        trial.TrialEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(10));

        // Verify status reflects the trial
        var statusResponse = await client.GetAsync("/subscriptions/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await statusResponse.Content.ReadFromJsonAsync<SubscriptionStatusResponse>();
        status.Should().NotBeNull();
        status!.Status.Should().Be("trial");

        // Second trial activation should conflict
        var conflictResponse = await client.PostAsync("/subscriptions/trial", null);
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
