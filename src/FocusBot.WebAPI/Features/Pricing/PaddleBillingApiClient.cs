using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using FocusBot.WebAPI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FocusBot.WebAPI.Features.Pricing;

/// <summary>
/// Calls Paddle Billing API for prices and customer portal sessions.
/// </summary>
public sealed class PaddleBillingApiClient(
    HttpClient httpClient,
    IOptions<PaddleSettings> paddleOptions,
    IMemoryCache cache,
    ILogger<PaddleBillingApiClient> logger
) : IPaddleBillingApi
{
    private PaddleSettings Settings => paddleOptions.Value;

    private string PricingCacheKey => $"paddle_pricing_v2_{Settings.CatalogProductId.Trim()}";

    public async Task<PricingResponse?> GetPricingAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            PricingCacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await FetchPricingUncachedAsync(ct);
            }
        );
    }

    private async Task<PricingResponse?> FetchPricingUncachedAsync(CancellationToken ct)
    {
        if (
            string.IsNullOrWhiteSpace(Settings.ApiBase)
            || string.IsNullOrWhiteSpace(Settings.ApiKey)
        )
        {
            logger.LogWarning("Paddle ApiBase or ApiKey is not configured; pricing unavailable.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(Settings.CatalogProductId))
        {
            logger.LogWarning("Paddle CatalogProductId is not configured; pricing unavailable.");
            return null;
        }

        var baseUrl = Settings.ApiBase.TrimEnd('/');
        var productId = Uri.EscapeDataString(Settings.CatalogProductId.Trim());
        var plans = new List<PricingPlanDto>();
        string? after = null;

        do
        {
            var url = $"{baseUrl}/prices?status=active&per_page=50&product_id={productId}";
            if (!string.IsNullOrEmpty(after))
                url += $"&after={Uri.EscapeDataString(after)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                Settings.ApiKey
            );

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Paddle list prices failed: {Status} {Body}",
                    response.StatusCode,
                    body.Length > 500 ? body[..500] : body
                );
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("data", out var dataEl))
                break;

            foreach (var price in dataEl.EnumerateArray())
            {
                var dto = MapPrice(price);
                if (dto is not null)
                    plans.Add(dto);
            }

            after = null;
            if (
                doc.RootElement.TryGetProperty("meta", out var meta)
                && meta.TryGetProperty("pagination", out var pagination)
                && pagination.TryGetProperty("has_more", out var hasMore)
                && hasMore.GetBoolean()
                && pagination.TryGetProperty("next", out var nextProp)
            )
            {
                var next = nextProp.GetString();
                if (!string.IsNullOrEmpty(next))
                    after = next;
            }
        } while (!string.IsNullOrEmpty(after));

        return new PricingResponse(plans, Settings.ClientToken ?? string.Empty, Settings.IsSandbox);
    }

    private static PricingPlanDto? MapPrice(JsonElement price)
    {
        if (price.GetProperty("status").GetString() != "active")
            return null;

        var id = price.GetProperty("id").GetString();
        if (string.IsNullOrEmpty(id))
            return null;

        var name = price.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? id : id;
        var description = price.TryGetProperty("description", out var descEl)
            ? descEl.GetString()
            : null;

        long unitMinor = 0;
        string currency = "USD";
        if (price.TryGetProperty("unit_price", out var unitPrice))
        {
            if (unitPrice.TryGetProperty("amount", out var amt))
            {
                var amtStr =
                    amt.ValueKind == JsonValueKind.String ? amt.GetString() : amt.GetRawText();
                _ = long.TryParse(amtStr, CultureInfo.InvariantCulture, out unitMinor);
            }

            if (unitPrice.TryGetProperty("currency_code", out var cur))
                currency = cur.GetString() ?? currency;
        }

        string? interval = null;
        if (
            price.TryGetProperty("billing_cycle", out var cycle)
            && cycle.ValueKind == JsonValueKind.Object
            && cycle.TryGetProperty("interval", out var intervalEl)
        )
            interval = intervalEl.GetString();

        var planType = "";
        if (
            price.TryGetProperty("custom_data", out var custom)
            && custom.ValueKind == JsonValueKind.Object
        )
        {
            if (custom.TryGetProperty("plan_type", out var pt))
                planType = pt.GetString() ?? "";
            if (string.IsNullOrEmpty(planType) && custom.TryGetProperty("license", out var licEl))
            {
                planType = licEl.GetString() switch
                {
                    "premium" => "cloud-managed",
                    "byok" => "cloud-byok",
                    _ => "",
                };
            }
        }

        if (string.IsNullOrEmpty(planType))
            return null;

        return new PricingPlanDto(id, name, description, unitMinor, currency, interval, planType);
    }

    public async Task<string?> CreateCustomerPortalSessionAsync(
        string paddleCustomerId,
        string? paddleSubscriptionId,
        CancellationToken ct = default
    )
    {
        if (
            string.IsNullOrWhiteSpace(Settings.ApiBase)
            || string.IsNullOrWhiteSpace(Settings.ApiKey)
        )
            return null;

        var baseUrl = Settings.ApiBase.TrimEnd('/');
        var url = $"{baseUrl}/customers/{Uri.EscapeDataString(paddleCustomerId)}/portal-sessions";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey);

        Dictionary<string, string[]?> body = new();
        if (!string.IsNullOrEmpty(paddleSubscriptionId))
            body["subscription_ids"] = [paddleSubscriptionId];

        request.Content = JsonContent.Create(
            body,
            options: new JsonSerializerOptions { PropertyNamingPolicy = null }
        );

        using var response = await httpClient.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Paddle portal session failed: {Status} {Body}",
                response.StatusCode,
                raw.Length > 500 ? raw[..500] : raw
            );
            return null;
        }

        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return null;

        if (
            data.TryGetProperty("urls", out var urls)
            && urls.TryGetProperty("general", out var general)
        )
        {
            var u = general.GetString();
            if (!string.IsNullOrEmpty(u))
                return u;
        }

        if (data.TryGetProperty("url", out var urlEl))
        {
            var u = urlEl.GetString();
            if (!string.IsNullOrEmpty(u))
                return u;
        }

        return null;
    }
}

public sealed record PricingPlanDto(
    string PriceId,
    string Name,
    string? Description,
    long UnitAmountMinor,
    string Currency,
    string? BillingInterval,
    string PlanType
);

public sealed record PricingResponse(
    IReadOnlyList<PricingPlanDto> Plans,
    string ClientToken,
    bool IsSandbox
);
