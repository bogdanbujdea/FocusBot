using System.Net;

namespace FocusBot.WebAPI.IntegrationTests;

public class AuthTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task AuthMe_Returns401_WithoutToken()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
