# Foqus WebAPI

ASP.NET Core Minimal API with vertical slice architecture, PostgreSQL, and SignalR real-time hub. Serves as the backend for all Foqus clients (desktop app, browser extension, web app).

**Deployed**: Azure Container App at `api.foqus.me`

---

## Architecture

### Vertical Slice Architecture

Each feature is self-contained in its own folder under `Features/`:

```
Features/<Name>/
├── SLICE.md            # Documentation
├── <Name>Endpoints.cs  # Minimal API route definitions
├── <Name>Service.cs    # Business logic
└── <Name>Dtos.cs       # Request/response DTOs
```

### Feature Slices (8)

| Slice | Purpose | SLICE.md |
|---|---|---|
| **Auth** | User profile + auto-provisioning from JWT | Yes |
| **Analytics** | Aggregated metrics, trends, per-client breakdown | Yes |
| **Classification** | AI alignment classification + server-side caching | Yes |
| **Clients** | Device registration, fingerprint-based upsert | Yes |
| **Pricing** | Paddle price proxy (anonymous, returns active prices + client token) | Yes |
| **Sessions** | Focus session lifecycle (start/end/pause/resume) | Yes |
| **Subscriptions** | Trial activation, status, Paddle webhook receiver | Yes |
| **Waitlist** | MailerLite email signup | No |

---

## API Endpoints

| Method | Route | Auth | Slice | Description |
|---|---|---|---|---|
| GET | `/` | No | — | Health check (returns `"Foqus API"`) |
| GET | `/health` | No | — | ASP.NET health checks |
| GET | `/auth/me` | Yes | Auth | User profile + auto-provisioning |
| DELETE | `/auth/account` | Yes | Auth | Permanently delete user account and all data |
| POST | `/sessions` | Yes | Sessions | Start a new focus session |
| POST | `/sessions/{id}/end` | Yes | Sessions | End active session with metrics |
| POST | `/sessions/{id}/pause` | Yes | Sessions | Pause active session |
| POST | `/sessions/{id}/resume` | Yes | Sessions | Resume paused session |
| GET | `/sessions/active` | Yes | Sessions | Get current active session |
| GET | `/sessions` | Yes | Sessions | Paginated completed session history |
| GET | `/sessions/{id}` | Yes | Sessions | Single session by ID |
| POST | `/classify` | Yes | Classification | AI alignment classification |
| POST | `/classify/validate-key` | Yes | Classification | Validate BYOK API key |
| GET | `/subscriptions/status` | Yes | Subscriptions | Current subscription status |
| POST | `/subscriptions/trial` | Yes | Subscriptions | Activate 24h trial |
| POST | `/subscriptions/portal` | Yes | Subscriptions | Paddle customer portal URL |
| POST | `/subscriptions/paddle-webhook` | No | Subscriptions | Paddle webhook receiver |
| POST | `/clients` | Yes | Clients | Register/re-register a client |
| GET | `/clients` | Yes | Clients | List user's clients |
| DELETE | `/clients/{id}` | Yes | Clients | Deregister a client |
| GET | `/analytics/summary` | Yes | Analytics | Aggregated metrics (date range, optional clientId) |
| GET | `/analytics/trends` | Yes | Analytics | Time-series trend data (granularity param) |
| GET | `/analytics/clients` | Yes | Analytics | Per-client breakdown |
| GET | `/pricing` | No | Pricing | Active Paddle prices + client token |
| POST | `/api/waitlist` | No* | Waitlist | MailerLite signup (*rate-limited: 5/min per IP) |

### SignalR Hub

| Path | Auth | Transport |
|---|---|---|
| `/hubs/focus` | Yes (JWT via `access_token` query param) | WebSocket / SSE |

---

## Database Schema

PostgreSQL via EF Core (Npgsql). Auto-migrates on startup (skipped in `Testing` environment).

### Tables

| Table | Entity | Key | Purpose |
|---|---|---|---|
| `Users` | `User` | `Id` (Guid) | User profiles, `Email` (unique, max 320) |
| `Sessions` | `Session` | `Id` (Guid) | Focus sessions. FK `UserId`, FK `ClientId?`. Unique filtered index on `UserId` where `EndedAtUtc IS NULL` (one active session per user) |
| `ClassificationCaches` | `ClassificationCache` | `Id` (Guid) | Server-side classification cache. FK `UserId`. Composite index on `(UserId, ContextHash, TaskContentHash)`. 24h TTL |
| `Subscriptions` | `Subscription` | `Id` (Guid) | Subscription plans. FK `UserId` (unique 1:1). Paddle fields, trial fields |
| `Clients` | `Client` | `Id` (Guid) | Registered client installations. FK `UserId`. Unique index on `(UserId, Fingerprint)` |
| `ProcessedWebhookEvents` | `ProcessedWebhookEvent` | `EventId` (string, 100) | Paddle webhook idempotency |

### Enums

| Enum | Values | Storage |
|---|---|---|
| `PlanType` | `TrialFullAccess` (0), `CloudBYOK` (1), `CloudManaged` (2) | Integer |
| `SubscriptionStatus` | `None`, `Trial`, `Active`, `Expired`, `Canceled` | camelCase string |
| `ClientType` | `Desktop` (1), `Extension` (2) | Integer |
| `ClientHost` | `Unknown` (0), `Windows` (1), `Chrome` (2), `Edge` (3) | Integer |

---

## SignalR Hub

`FocusHub` at `/hubs/focus` — typed client interface `IFocusHubClient`:

| Event | Record | Fields | Trigger |
|---|---|---|---|
| `SessionStarted` | `SessionStartedEvent` | `SessionId`, `SessionTitle`, `SessionContext?`, `StartedAtUtc`, `Source` | `POST /sessions` |
| `SessionEnded` | `SessionEndedEvent` | `SessionId`, `EndedAtUtc`, `Source` | `POST /sessions/{id}/end` |
| `SessionPaused` | `SessionPausedEvent` | `SessionId`, `PausedAtUtc`, `Source` | `POST /sessions/{id}/pause` |
| `SessionResumed` | `SessionResumedEvent` | `SessionId`, `Source` | `POST /sessions/{id}/resume` |
| `PlanChanged` | `PlanChangedEvent` | *(empty)* | Paddle webhook processing |
| `ClassificationChanged` | `ClassificationChangedEvent` | `Score`, `Reason`, `Source`, `ActivityName`, `ClassifiedAtUtc`, `Cached` | `POST /classify` |

**Server-side**: `OnConnectedAsync` adds connection to per-user group (userId from JWT `sub` claim). `OnDisconnectedAsync` removes from group.

---

## Authentication

Supabase JWT validation (ES256 via JWKS):
- `JwksRefreshService` (singleton background service) refreshes JWKS every 5 minutes
- JWT `sub` claim maps to user ID
- First `GET /auth/me` auto-provisions `User` row + 24h trial `Subscription` row
- Concurrent first-requests handled via catch-and-retry on unique constraint violation

---

## Registered Services

### Scoped Services

| Service | Slice |
|---|---|
| `AuthService` | Auth — user lookup/provisioning |
| `AccountService` | Auth — account operations |
| `SessionService` | Sessions — CRUD + pause/resume |
| `ClassificationService` | Classification — LLM classification + caching |
| `SubscriptionService` | Subscriptions — trial, status, webhook handling |
| `WaitlistService` | Waitlist — MailerLite integration |
| `ClientService` | Clients — registration, heartbeat, deletion |
| `AnalyticsService` | Analytics — aggregation queries |

### Singletons

| Service | Purpose |
|---|---|
| `JwksRefreshService` | Background JWKS refresh (every 5 min) |

### Infrastructure

| Registration | Purpose |
|---|---|
| `AddDbContext<ApiDbContext>` | PostgreSQL via Npgsql |
| `AddAuthentication` + `AddJwtBearer` | Supabase JWT (ES256) |
| `AddSignalR` | Real-time events |
| `AddCors("Frontend")` | Configurable allowed origins with credentials |
| `AddRateLimiter("Waitlist")` | 5 req/min per IP fixed window |
| `AddOpenApi` | OpenAPI document generation |
| `AddExceptionHandler<GlobalExceptionHandler>` | Global exception → ProblemDetails (RFC 9457) |
| `AddHealthChecks` | `/health` endpoint |
| `AddMemoryCache` | In-memory caching |
| `AddHttpClient(WaitlistService.HttpClientName)` | Named HttpClient for MailerLite |
| `AddHttpClient<IPaddleBillingApi, PaddleBillingApiClient>` | Typed HttpClient for Paddle Billing API |

### Middleware Pipeline

1. `UseExceptionHandler()` (GlobalExceptionHandler)
2. `MapOpenApi()` + `MapScalarApiReference()` (Scalar API docs)
3. `UseStaticFiles()` (`wwwroot/`)
4. `UseCors("Frontend")`
5. `UseRateLimiter()`
6. `UseAuthentication()`
7. `UseAuthorization()`
8. Endpoint routing (hub + health + feature endpoints)
9. Auto-migration on startup (skipped in `Testing`)

---

## Paddle Webhook Processing

### Flow

1. `POST /subscriptions/paddle-webhook` receives raw body
2. `PaddleWebhookVerifier` validates HMAC-SHA256 signature using `Paddle:WebhookSecret`
3. Check `ProcessedWebhookEvents` for idempotency (dedup by `event_id`)
4. Parse strongly-typed `PaddleWebhookModels`
5. Update `Subscription` row in database
6. Broadcast `PlanChanged` via SignalR hub
7. Insert `event_id` into `ProcessedWebhookEvents`

### Security

- Signature verification rejects all requests when `Paddle:WebhookSecret` is not configured (no dev bypass)
- All events deduplicated by `event_id`
- `past_due` Paddle status maps to `Expired` (no access)

### Handled Events

| Paddle Event | Database Action |
|---|---|
| `subscription.created` | Create/update Subscription |
| `subscription.updated` | Update plan type, status, dates |
| `subscription.canceled` | Set status to `Canceled` |
| `transaction.completed` | Update payment details |

---

## Docker

### Dockerfile

Two-stage build:
- **Build**: `mcr.microsoft.com/dotnet/sdk:10.0` → restore, publish Release
- **Runtime**: `mcr.microsoft.com/dotnet/aspnet:10.0` → expose port 8080

### docker-compose.yml

```yaml
services:
  postgres:   # postgres:16-alpine, port 5432, health check, named volume
  api:        # builds from Dockerfile, port 5251→8080, depends on postgres
```

---

## Configuration

### appsettings.json

| Section | Keys | Purpose |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string | Database |
| `Supabase:Url` | Supabase project URL | JWT validation |
| `Supabase:JwksUrl` | JWKS endpoint | Key refresh |
| `Paddle:ApiBase` | Paddle API URL | Webhook handling |
| `Paddle:IsSandbox` | Boolean | Environment flag |
| `Paddle:ClientToken` | Paddle client token | Paddle.js (returned to web app) |
| `Paddle:CatalogProductId` | `pro_...` product ID | Product filtering |
| `Cors:AllowedOrigins` | Origin array | CORS configuration |
| `MailerLite:ApiKey` | API key | Waitlist integration |

### User Secrets (sensitive)

```bash
dotnet user-secrets set "Paddle:ApiKey" "<server-api-key>" --project src/FocusBot.WebAPI
dotnet user-secrets set "Paddle:WebhookSecret" "<signing-secret>" --project src/FocusBot.WebAPI
dotnet user-secrets set "Paddle:CatalogProductId" "pro_..." --project src/FocusBot.WebAPI
dotnet user-secrets set "Paddle:ClientToken" "<client-token>" --project src/FocusBot.WebAPI
```

---

## Building and Testing

```bash
# Build
dotnet build src/FocusBot.WebAPI/FocusBot.WebAPI.csproj

# Run (requires PostgreSQL)
dotnet run --project src/FocusBot.WebAPI/FocusBot.WebAPI.csproj --launch-profile http

# Unit tests (~82 tests)
dotnet test tests/FocusBot.WebAPI.Tests/FocusBot.WebAPI.Tests.csproj

# Integration tests (~32 tests, WebApplicationFactory + InMemory DB)
dotnet test tests/FocusBot.WebAPI.IntegrationTests/FocusBot.WebAPI.IntegrationTests.csproj
```

Integration tests use `CustomWebApplicationFactory` which provides test JWT config and swaps Npgsql for InMemory DB.

---

## Slice Documentation

Each feature slice has a `SLICE.md` in its folder with detailed endpoint specs, request/response formats, and business rules. See:

- `Features/Auth/SLICE.md`
- `Features/Analytics/SLICE.md`
- `Features/Classification/SLICE.md`
- `Features/Clients/SLICE.md`
- `Features/Pricing/SLICE.md`
- `Features/Sessions/SLICE.md`
- `Features/Subscriptions/SLICE.md`
