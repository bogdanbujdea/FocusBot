using System.Net;
using System.Net.Http.Headers;
using FocusBot.WebAPI.Data;
using Microsoft.Extensions.DependencyInjection;

namespace FocusBot.WebAPI.IntegrationTests;

public class AuthTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task AuthMe_Returns401_WithoutToken()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthMe_CreatesUserRow_OnFirstCall()
    {
        // Arrange — simulate what the desktop does immediately after magic-link callback
        var userId = Guid.NewGuid();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtHelper.GenerateTestJwt(userId, "newuser@example.com"));

        // Act
        var response = await client.GetAsync("/auth/me");

        // Assert — HTTP response succeeds
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — user row was created in the database
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var user = db.Users.FirstOrDefault(u => u.Id == userId);
        user.Should().NotBeNull();
        user!.Email.Should().Be("newuser@example.com");
    }

    [Fact]
    public async Task AuthMe_ReturnsOk_WhenCalledTwice()
    {
        // Arrange — simulates session restore on subsequent launches calling /auth/me again
        var userId = Guid.NewGuid();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtHelper.GenerateTestJwt(userId));

        // Act — first call provisions; second call must not fail or duplicate
        var first = await client.GetAsync("/auth/me");
        var second = await client.GetAsync("/auth/me");

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        db.Users.Count(u => u.Id == userId).Should().Be(1);
    }
}
