using System.Security.Claims;

namespace FocusBot.WebAPI.Features.Analytics;

/// <summary>
/// Minimal API endpoints for analytics aggregation. All endpoints require authentication.
/// </summary>
public static class AnalyticsEndpoints
{
    public static RouteGroupBuilder MapAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/analytics")
            .WithTags("Analytics")
            .RequireAuthorization();

        group.MapGet("/summary", GetSummary)
            .WithName("GetAnalyticsSummary")
            .WithSummary("Aggregated focus metrics for a date range");

        group.MapGet("/trends", GetTrends)
            .WithName("GetAnalyticsTrends")
            .WithSummary("Time-series trend data for charts");

        group.MapGet("/devices", GetDeviceBreakdown)
            .WithName("GetAnalyticsDevices")
            .WithSummary("Per-device analytics breakdown");

        return group;
    }

    private static async Task<IResult> GetSummary(
        AnalyticsService service,
        HttpContext ctx,
        DateTime? from = null,
        DateTime? to = null,
        Guid? deviceId = null,
        CancellationToken ct = default
    )
    {
        var userId = GetUserId(ctx);
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;

        var result = await service.GetSummaryAsync(userId, fromDate, toDate, deviceId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTrends(
        AnalyticsService service,
        HttpContext ctx,
        DateTime? from = null,
        DateTime? to = null,
        string granularity = "daily",
        Guid? deviceId = null,
        CancellationToken ct = default
    )
    {
        var userId = GetUserId(ctx);
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var result = await service.GetTrendsAsync(userId, fromDate, toDate, granularity, deviceId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetDeviceBreakdown(
        AnalyticsService service,
        HttpContext ctx,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default
    )
    {
        var userId = GetUserId(ctx);
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var result = await service.GetDeviceBreakdownAsync(userId, fromDate, toDate, ct);
        return Results.Ok(result);
    }

    private static Guid GetUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? ctx.User.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("JWT missing sub claim");
        return Guid.Parse(sub);
    }
}
