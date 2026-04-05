using System.Net;
using System.Security.Claims;

namespace FocusBot.WebAPI.Features.Clients;

/// <summary>
/// Minimal API endpoints for client registration and management.
/// </summary>
public static class ClientsEndpoints
{
    public static RouteGroupBuilder MapClientsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/clients").WithTags("Clients").RequireAuthorization();

        group
            .MapPost(
                "/",
                async (
                    RegisterClientRequest request,
                    ClientService service,
                    HttpContext ctx,
                    CancellationToken ct
                ) =>
                {
                    var userId = GetUserId(ctx);

                    if (string.IsNullOrWhiteSpace(request.Name))
                        return Results.BadRequest("Name is required.");

                    if (string.IsNullOrWhiteSpace(request.Fingerprint))
                        return Results.BadRequest("Fingerprint is required.");

                    var remoteIp = GetRemoteIpAddress(ctx);
                    var client = await service.RegisterAsync(userId, request, remoteIp, ct);
                    return Results.Created($"/clients/{client.Id}", client);
                }
            )
            .WithName("RegisterClient")
            .WithSummary("Register or re-register a software client");

        group
            .MapGet(
                "/",
                async (ClientService service, HttpContext ctx, CancellationToken ct) =>
                {
                    var userId = GetUserId(ctx);
                    var clients = await service.GetClientsAsync(userId, ct);
                    return Results.Ok(clients);
                }
            )
            .WithName("GetClients")
            .WithSummary("List all registered clients for the current user");

        group
            .MapDelete(
                "/{id:guid}",
                async (Guid id, ClientService service, HttpContext ctx, CancellationToken ct) =>
                {
                    var userId = GetUserId(ctx);
                    var deleted = await service.DeleteAsync(userId, id, ct);

                    return deleted ? Results.NoContent() : Results.NotFound();
                }
            )
            .WithName("DeleteClient")
            .WithSummary("Deregister a client (e.g. on explicit logout)");

        return group;
    }

    private static Guid GetUserId(HttpContext ctx)
    {
        var sub =
            ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? ctx.User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT missing sub claim");
        return Guid.Parse(sub);
    }

    private static string? GetRemoteIpAddress(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress;
        if (ip is null)
            return null;

        if (
            ip is
            {
                AddressFamily: System.Net.Sockets.AddressFamily.InterNetworkV6,
                IsIPv4MappedToIPv6: true
            }
        )
        {
            return ip.MapToIPv4().ToString();
        }

        return ip.ToString();
    }
}
