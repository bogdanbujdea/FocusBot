using System.Security.Claims;

namespace FocusBot.WebAPI.Features.Devices;

/// <summary>
/// Minimal API endpoints for device registration, heartbeat, and management.
/// </summary>
public static class DevicesEndpoints
{
    public static RouteGroupBuilder MapDevicesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/devices")
            .WithTags("Devices")
            .RequireAuthorization();

        group.MapPost("/", async (
            RegisterDeviceRequest request,
            DeviceService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx);

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest("Name is required.");

            if (string.IsNullOrWhiteSpace(request.Fingerprint))
                return Results.BadRequest("Fingerprint is required.");

            var device = await service.RegisterAsync(userId, request, ct);
            return Results.Created($"/devices/{device.Id}", device);
        })
        .WithName("RegisterDevice")
        .WithSummary("Register or re-register a client device");

        group.MapGet("/", async (
            DeviceService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx);
            var devices = await service.GetDevicesAsync(userId, ct);
            return Results.Ok(devices);
        })
        .WithName("GetDevices")
        .WithSummary("List all registered devices for the current user");

        group.MapPut("/{id:guid}/heartbeat", async (
            Guid id,
            HeartbeatRequest request,
            DeviceService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx);
            var device = await service.HeartbeatAsync(userId, id, request, ct);

            return device is not null ? Results.Ok(device) : Results.NotFound();
        })
        .WithName("Heartbeat")
        .WithSummary("Send a heartbeat to mark the device as online and update version info");

        group.MapDelete("/{id:guid}", async (
            Guid id,
            DeviceService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx);
            var deleted = await service.DeleteAsync(userId, id, ct);

            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteDevice")
        .WithSummary("Deregister a device (e.g. on explicit logout)");

        return group;
    }

    private static Guid GetUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? ctx.User.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("JWT missing sub claim");
        return Guid.Parse(sub);
    }
}
