using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FocusBot.WebAPI.Features.Pricing;

namespace FocusBot.WebAPI.IntegrationTests;

public class PricingAndPortalTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task GetPricing_ReturnsPlans_Anonymous()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/pricing");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PricingResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body.Should().NotBeNull();
        body!.Plans.Should().HaveCount(2);
        body.ClientToken.Should().Be("test_client_token");
        body.IsSandbox.Should().BeTrue();
    }

    [Fact]
    public async Task PostPortal_Returns400_WhenNoPaddleCustomer()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtHelper.GenerateTestJwt(userId));

        var me = await client.GetAsync("/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsync("/subscriptions/portal", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
