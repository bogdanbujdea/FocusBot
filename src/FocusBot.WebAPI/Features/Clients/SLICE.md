# Clients Slice

Manages device/client registration for multi-device session tracking and analytics.

## Concept

A **client** is one registered software installation (WinUI app, Chrome extension, Edge extension, etc.), not one physical machine. Each install has a stable fingerprint and a server-assigned client ID. Sessions and analytics reference `clientId` for per-device breakdown.

## Endpoints

All endpoints require authorization.

| Method | Route | Name | Description |
|---|---|---|---|
| POST | `/clients` | RegisterClient | Register or re-register a client (upsert by fingerprint) |
| GET | `/clients` | GetClients | List all registered clients for the current user |
| DELETE | `/clients/{id:guid}` | DeleteClient | Deregister a client (e.g. on explicit logout) |

### POST `/clients`

**Request**: `RegisterClientRequest`

| Field | Type | Required | Description |
|---|---|---|---|
| `clientType` | `ClientType` enum | Yes | `DesktopApp` or `Extension` |
| `host` | `ClientHost` enum | Yes | `Unknown`, `Windows`, `Chrome`, `Edge` |
| `name` | string | Yes | Display name (e.g. "Work PC - Chrome") |
| `fingerprint` | string | Yes | Stable install fingerprint |
| `appVersion` | string? | No | Client version |
| `platform` | string? | No | OS/platform info |

**Behavior**: Upserts — if a client with the same `userId` + `fingerprint` exists, updates fields; otherwise creates new.

**Response**: `201 Created` with `ClientResponse`

### GET `/clients`

Returns all clients registered to the authenticated user.

**Response**: `200 OK` with `ClientResponse[]`

### DELETE `/clients/{id}`

Returns `204 No Content` on success, `404 Not Found` if client doesn't exist or belongs to another user.

## DTOs

```csharp
record RegisterClientRequest(ClientType ClientType, ClientHost Host, string Name, string Fingerprint, string? AppVersion, string? Platform);

record ClientResponse(Guid Id, ClientType ClientType, ClientHost Host, string Name, string Fingerprint, string? AppVersion, string? Platform, string? IpAddress, DateTime LastSeenAtUtc, DateTime CreatedAtUtc, bool IsOnline);
```

## Enums

- `ClientType`: `DesktopApp`, `Extension`
- `ClientHost`: `Unknown`, `Windows`, `Chrome`, `Edge`

## IP Address Tracking

`IpAddress` on the client row is captured from `HttpContext.Connection.RemoteIpAddress` on register. IPv4-mapped IPv6 addresses are normalized to IPv4.

## Files

| File | Purpose |
|---|---|
| `ClientsEndpoints.cs` | Minimal API route definitions |
| `ClientService.cs` | Business logic (register, list, delete) |
| `ClientDtos.cs` | Request/response DTOs |
