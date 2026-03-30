using System.Net;
using System.Text;
using FocusBot.WebAPI.Features.Pricing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FocusBot.WebAPI.Tests.Features.Pricing;

public class PaddleBillingApiClientTests
{
    [Fact]
    public async Task CreateCustomerPortalSessionAsync_ReturnsOverviewUrl_FromNestedGeneralObject()
    {
        const string payload =
            """
            {
              "data": {
                "id": "cpls_123",
                "customer_id": "ctm_123",
                "urls": {
                  "general": {
                    "overview": "https://sandbox-customer-portal.paddle.com/example-overview"
                  }
                }
              },
              "meta": { "request_id": "req_123" }
            }
            """;

        var client = CreateClient(payload);

        var url = await client.CreateCustomerPortalSessionAsync("ctm_123", "sub_123");

        url.Should().Be("https://sandbox-customer-portal.paddle.com/example-overview");
    }

    [Fact]
    public async Task CreateCustomerPortalSessionAsync_FallsBackToDataUrl_WhenOverviewMissing()
    {
        const string payload =
            """
            {
              "data": {
                "id": "cpls_123",
                "customer_id": "ctm_123",
                "url": "https://sandbox-customer-portal.paddle.com/fallback-url"
              }
            }
            """;

        var client = CreateClient(payload);

        var url = await client.CreateCustomerPortalSessionAsync("ctm_123", "sub_123");

        url.Should().Be("https://sandbox-customer-portal.paddle.com/fallback-url");
    }

    [Fact]
    public async Task CreateCustomerPortalSessionAsync_ReturnsNull_OnInvalidJson()
    {
        const string payload = "{ \"data\": ";
        var client = CreateClient(payload);

        var url = await client.CreateCustomerPortalSessionAsync("ctm_123", "sub_123");

        url.Should().BeNull();
    }

    private static PaddleBillingApiClient CreateClient(string responseBody)
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            }
        );

        var httpClient = new HttpClient(handler);
        var options = Options.Create(
            new PaddleSettings
            {
                ApiBase = "https://sandbox-api.paddle.com",
                ApiKey = "pdl_sdbx_test",
                CatalogProductId = "pro_test",
                ClientToken = "test_client_token",
                IsSandbox = true,
            }
        );
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = LoggerFactory
            .Create(builder => { })
            .CreateLogger<PaddleBillingApiClient>();

        return new PaddleBillingApiClient(httpClient, options, cache, logger);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(responder(request));
    }
}
