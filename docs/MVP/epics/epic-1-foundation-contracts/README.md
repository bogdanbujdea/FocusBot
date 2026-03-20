# Epic 1 — Shared Contracts & Entitlement Model

## Objective

Define all shared contracts, terminology, schemas, and entitlement rules before any client implementation begins. Every client (Windows app, browser extension, web app) and the backend must reference these definitions.

**No client starts implementation without using these shared definitions.**

---

## Deliverables

1. Plan definitions with feature matrix and Paddle product mapping
2. Basic analytics specification
3. Full analytics specification
4. Device model and entity schema
5. Heartbeat contract
6. Classification routing decision table
7. Cloud BYOK data flow design decision
8. Event schema for analytics/session data
9. API versioning strategy
10. Token lifecycle contract
11. Backend endpoint inventory (new vs existing)
12. Server-mediated real-time push contract (SignalR)
13. Shared entitlement rules

---

## 1. Plan Definitions

### Feature Matrix

| Feature | Free (BYOK) | Cloud BYOK | Cloud Managed |
|---|---|---|---|
| Account required | No | Yes | Yes |
| API key source | User-provided | User-provided | Platform-managed |
| Classification route | Client → provider | Client → provider | Client → backend → provider |
| Basic analytics | Yes (local) | Yes (local + cloud) | Yes (local + cloud) |
| Full analytics | No | Yes (web app) | Yes (web app) |
| Cross-device sync | No | Yes | Yes |
| Device registration | No | Yes | Yes |
| Session cloud storage | No | Yes | Yes |
| Paddle product | None | `cloud_byok_monthly` | `cloud_managed_monthly` |

### Paddle Product Mapping

- **Free (BYOK):** No Paddle product. No subscription entity required.
- **Cloud BYOK:** Paddle product `cloud_byok_monthly`. Lower price tier. User brings their own LLM API key.
- **Cloud Managed:** Paddle product `cloud_managed_monthly`. Higher price tier. Platform provides the LLM API key.

> **Migration note:** The existing `Subscription` entity has `PaddleSubscriptionId` and `PaddleCustomerId` fields. The current single premium tier must be mapped or migrated to the two new tiers. Define a migration strategy before launching new products in Paddle.

### Tasks

- [ ] Create Paddle products for Cloud BYOK and Cloud Managed
- [ ] Define migration path for existing premium subscribers
- [ ] Update `Subscription` entity to include `PlanType` enum (`Free`, `CloudByok`, `CloudManaged`)
- [ ] Update `/subscriptions/status` response to include plan type
- [ ] Update trial logic — does trial apply to Cloud BYOK, Cloud Managed, or both?

---

## 2. Basic Analytics Specification

Basic analytics are available to **all** users (including free) and are computed/stored **locally** by each client.

### Included Metrics

| Metric | Description |
|---|---|
| Session list | Recent sessions with task name, start/end time, duration |
| Session duration | Total time per session |
| Aligned time | Total seconds classified as aligned per session |
| Distracted time | Total seconds classified as not aligned per session |
| Focus score | Time-weighted alignment percentage (0–100) per session |
| Distraction count | Number of transitions from aligned to not-aligned per session |
| Simple counts | Total sessions, total focused time, total distracted time (aggregate) |
| Last 7 days summary | Daily totals for the above metrics, local only |

### What Is NOT Basic Analytics

- Trends over time beyond 7 days
- Multi-device aggregation
- Deeper charts / filters / comparisons
- Cloud history
- Per-task breakdown across sessions
- Top distractions ranked over time

### Already Implemented

| Client | Status |
|---|---|
| Windows app | `[EXISTS]` Daily analytics (focused time, distraction count, top apps, avg distraction cost, longest focused session). Session history with Focus Score. |
| Browser extension | `[EXISTS]` Session summaries (focus %, aligned/distracting seconds, distraction count, context switch cost, top domains). Today/7d/30d aggregations. |

### Tasks

- [ ] Audit existing Windows app analytics against this spec — identify gaps
- [ ] Audit existing extension analytics against this spec — identify gaps
- [ ] Define the canonical "basic analytics" data model shared across clients (field names, types, units)

---

## 3. Full Analytics Specification

Full analytics are available only to **paid cloud users** (Cloud BYOK and Cloud Managed) and live **exclusively in the web app** (`app.foqus.me`).

### Included Metrics

| Metric | Description |
|---|---|
| Trend charts | Focus vs distraction over time (daily, weekly, monthly) |
| Multi-device view | Analytics broken down by device/integration |
| By-task breakdown | Focus metrics grouped by task name |
| Session history | Paginated, filterable, sortable cloud session list |
| Daily / weekly summaries | Aggregated metrics per period |
| Top distractions | Ranked list of most distracting apps/sites over time |
| Top aligned activities | Ranked list of most aligned apps/sites over time |
| Comparisons | Period-over-period comparison (this week vs last week, etc.) |
| Filters | By date range, device, task, source |

### Data Source

Full analytics are computed from **synced session and classification data** in the backend. Clients submit data to the backend; the web app reads aggregated results via analytics API endpoints.

### Tasks

- [ ] Define analytics API endpoint structure (see §11)
- [ ] Define aggregation strategy (materialized views vs computed-on-read)
- [ ] Define data retention policy for cloud sessions

---

## 4. Device Model

### `Device` Entity Schema

```
Device
├── Id              : Guid (PK)
├── UserId          : Guid (FK → User)
├── DeviceType      : enum { Desktop, Extension }
├── Name            : string (e.g., "Work Laptop", "Chrome Extension")
├── Fingerprint     : string (unique per installation)
├── AppVersion      : string (e.g., "1.2.0")
├── Platform        : string (e.g., "Windows 11", "Chrome 120")
├── Status          : enum { Online, Offline }
├── LastSeenUtc     : DateTimeOffset
├── CreatedAtUtc    : DateTimeOffset
└── UpdatedAtUtc    : DateTimeOffset
```

### Fingerprint Strategy

- **Windows app:** Generate a stable GUID on first launch, persist in local settings. Survives app updates but not reinstalls.
- **Browser extension:** Generate a stable GUID on install, persist in `chrome.storage.local`. Survives extension updates but not reinstalls.

### `Session.DeviceId` Attribution

- Add `DeviceId` (nullable FK → `Device`) to the existing `Session` entity.
- Existing sessions with `Source = "api"` retain `DeviceId = null`.
- New sessions submitted by registered devices include the `DeviceId`.

### Tasks

- [ ] Create `Device` entity and EF Core migration
- [ ] Add `DeviceId` nullable FK to `Session` entity
- [ ] Create `DeviceType` enum
- [ ] Add `Devices` feature slice to WebAPI

---

## 5. Heartbeat Contract

### Endpoint

```
PUT /devices/{deviceId}/heartbeat
Authorization: Bearer <token>
```

### Request Body

```json
{
  "appVersion": "1.2.0",
  "platform": "Windows 11"
}
```

### Behavior

- Updates `LastSeenUtc` to current UTC time.
- Updates `Status` to `Online`.
- Updates `AppVersion` and `Platform` if changed.

### Heartbeat Interval

- Clients send a heartbeat every **60 seconds** while active.
- Backend marks a device as `Offline` if no heartbeat received for **3 minutes**.

### Offline Detection

- A background job or lazy check on read: if `LastSeenUtc` is older than 3 minutes, return `Status = Offline`.

### Tasks

- [ ] Implement `PUT /devices/{deviceId}/heartbeat` endpoint
- [ ] Add offline detection logic (lazy or background job)
- [ ] Define heartbeat timer in Windows app
- [ ] Define heartbeat timer in browser extension (service worker keep-alive considerations)

---

## 6. Classification Routing Decision Table

| Plan | Provider Mode | Route | Who Calls LLM? |
|---|---|---|---|
| Free (BYOK) | BYOK | Client → LLM provider directly | Client |
| Cloud BYOK | BYOK | Client → LLM provider directly | Client |
| Cloud Managed | Managed | Client → `POST /classify` → LLM provider | Backend |

### Notes

- The client determines the route based on the user's plan and whether an API key is configured.
- For Cloud BYOK, the client calls the provider directly but **also** submits classification/session data to the backend for full analytics and sync (see §7).
- For Cloud Managed, the existing `POST /classify` endpoint handles classification. The backend uses the managed API key.
- The existing `X-Api-Key` header passthrough on `POST /classify` is **not used** in the new model for Cloud BYOK — the client calls the provider directly instead. This simplifies the backend and avoids proxying user keys.

### Tasks

- [ ] Update classification routing logic in Windows app to be plan-aware
- [ ] Update classification routing logic in browser extension to be plan-aware
- [ ] Decide whether to deprecate the `X-Api-Key` passthrough on `POST /classify`
- [ ] Add abstraction in each client so UI/business logic does not care which route is active

---

## 7. Cloud BYOK Data Flow — Open Design Decision

> **This must be resolved before Epic 2 implementation begins.**

When a Cloud BYOK user classifies directly with their LLM provider, the backend never sees the classification result. How does the backend get the data needed for full analytics and sync?

### Option A: Session Summaries Only

- Clients submit a session summary at session end via `POST /sessions/{id}/end` (already implemented).
- Backend stores aggregate metrics (focus score, aligned time, distracted time, distraction count).
- **Pros:** Simple, low bandwidth, already partially implemented.
- **Cons:** No per-classification granularity in cloud analytics. Cannot show "at 2:15 PM you switched to Twitter" in the web dashboard.

### Option B: Events + Summaries

- Clients fire individual classification events to the backend in near-real-time (e.g., `POST /sessions/{id}/events`).
- Clients also submit the session summary at session end.
- **Pros:** Rich per-event analytics, "timeline view" possible, enables detailed distraction analysis.
- **Cons:** More bandwidth, more backend storage, more complexity, offline queue needed.

### Decision Criteria

- How important is per-event drill-down in full analytics?
- What is the acceptable data volume per user per day?
- Is near-real-time event submission feasible from the extension service worker?
- Can Option A be shipped first and Option B added later without breaking changes?

### Tasks

- [ ] Evaluate both options against full analytics requirements (§3)
- [ ] Prototype bandwidth/storage estimates for Option B
- [ ] **Decide and document** before Epic 2 begins

---

## 8. Event Schema

### Session Event (if Option B is chosen)

```json
{
  "eventId": "uuid",
  "sessionId": "uuid",
  "deviceId": "uuid",
  "timestamp": "2026-03-20T14:15:00Z",
  "eventType": "classification",
  "context": {
    "windowTitle": "Twitter - Home",
    "url": "https://twitter.com/home",
    "appName": "chrome.exe"
  },
  "result": {
    "status": "NotAligned",
    "confidence": 0.92,
    "explanation": "Social media browsing"
  }
}
```

### Session Summary (both options)

```json
{
  "sessionId": "uuid",
  "deviceId": "uuid",
  "taskTitle": "Write API docs",
  "startedAtUtc": "2026-03-20T14:00:00Z",
  "endedAtUtc": "2026-03-20T15:30:00Z",
  "focusScorePercent": 78,
  "focusedSeconds": 4200,
  "distractedSeconds": 1200,
  "distractionCount": 5,
  "contextSwitchCostSeconds": 120,
  "topDistractingApps": ["twitter.com", "reddit.com"],
  "source": "Desktop"
}
```

### Notes

- `deviceId` is required for all cloud submissions from registered devices.
- `eventId` must be a client-generated UUID for idempotency.
- Existing `POST /sessions/{id}/end` payload should be extended to match this schema.

### Tasks

- [ ] Finalize event schema based on §7 decision
- [ ] Define idempotency key strategy for event submission
- [ ] Update `POST /sessions/{id}/end` DTO to include `deviceId`

---

## 9. API Versioning Strategy

### Approach: Header-Based

```
Api-Version: 2026-03-20
```

- All existing endpoints are version `2025-01-01` (implicit, no header required for backward compatibility).
- New endpoints introduced in this rollout use version `2026-03-20`.
- Breaking changes to existing endpoints require a new version date.
- The backend reads the `Api-Version` header and routes to the appropriate handler.

### Alternatives Considered

- **URL prefix (`/v2/`):** Rejected — duplicates routes, harder to manage in minimal APIs.
- **No versioning:** Rejected — three clients shipping at different cadences makes breaking changes too risky.

### Tasks

- [ ] Add `Api-Version` header middleware to WebAPI
- [ ] Document versioning convention in coding guidelines
- [ ] Define deprecation policy (how long old versions are supported)

---

## 10. Token Lifecycle Contract

### Shared Rules (All Clients)

| Concern | Rule |
|---|---|
| Token source | Supabase JWT (ES256, same project across all clients) |
| Access token TTL | 1 hour (Supabase default) |
| Refresh strategy | Refresh when < 5 minutes remaining before expiry |
| Refresh failure | Retry 3 times with exponential backoff, then prompt re-login |
| Logout | Clear local tokens, revoke refresh token via Supabase |
| Session expiry | If refresh token is expired (7 days default), prompt re-login |

### Current Status

| Client | Refresh Implementation |
|---|---|
| Windows app | `[EXISTS]` `SupabaseAuthService.RefreshTokenAsync()` — manual HTTP call |
| Browser extension | `[EXISTS]` Supabase JS client handles refresh automatically |
| Web app | `[NEW]` Needs implementation — Supabase JS client recommended |
| WebAPI | `[EXISTS]` Validates JWT, no refresh needed server-side |

### Tasks

- [ ] Align Windows app refresh logic with the 5-minute threshold rule
- [ ] Verify extension Supabase JS client respects the same rules
- [ ] Implement token management in web app using Supabase JS client
- [ ] Add token expiry monitoring/logging in all clients

---

## 11. Backend Endpoint Inventory

### Existing Endpoints (may need changes)

| Endpoint | Slice | Changes Needed |
|---|---|---|
| `GET /auth/me` | Auth | Add `planType` to response |
| `POST /classify` | Classification | Review `X-Api-Key` passthrough — may deprecate |
| `POST /sessions` | Sessions | Add optional `deviceId` field |
| `POST /sessions/{id}/end` | Sessions | Add `deviceId`, extend payload per §8 schema |
| `GET /sessions/active` | Sessions | No changes |
| `GET /sessions` | Sessions | Add `deviceId` filter parameter |
| `GET /sessions/{id}` | Sessions | Include `deviceId` in response |
| `GET /subscriptions/status` | Subscriptions | Add `planType` to response |
| `POST /subscriptions/trial` | Subscriptions | Decide which plans trial applies to |
| `POST /subscriptions/paddle-webhook` | Subscriptions | **Complete signature verification**, handle new product IDs |

### New Endpoints

| Endpoint | Slice | Purpose |
|---|---|---|
| `POST /devices` | Devices | Register a device |
| `GET /devices` | Devices | List user's devices |
| `GET /devices/{id}` | Devices | Get single device |
| `DELETE /devices/{id}` | Devices | Unregister a device |
| `PUT /devices/{id}/heartbeat` | Devices | Heartbeat (see §5) |
| `POST /sessions/{id}/events` | Sessions | Submit classification events (if Option B, see §7) |
| `GET /analytics/summary` | Analytics | Aggregated metrics for a date range |
| `GET /analytics/trends` | Analytics | Time-series data for charts |
| `GET /analytics/devices` | Analytics | Per-device breakdown |

### Tasks

- [ ] Create `Devices` feature slice (entity, endpoints, service, DTOs, SLICE.md)
- [ ] Update existing slices per changes column
- [ ] Create `Analytics` feature slice (Epic 5, but define contract now)
- [ ] Complete Paddle webhook signature verification

---

## 12. Server-Mediated Real-Time Push (SignalR)

### Purpose

Replace the local-only WebSocket (`ws://localhost:9876/focusbot`) with a server-mediated SignalR hub for cloud users. This enables:

- Multi-machine task sync (home desktop + work desktop)
- Real-time presence updates on the web integrations page
- Live classification events pushed to the web dashboard
- Server-initiated notifications (plan changes, trial expiry)

### Hub Contract

```
Hub: /hubs/focus
Authentication: Bearer token (same Supabase JWT)
```

### Server → Client Messages

| Message | Payload | Trigger |
|---|---|---|
| `TaskStarted` | `{ sessionId, taskTitle, startedAtUtc }` | Any device starts a task |
| `TaskEnded` | `{ sessionId, summary }` | Any device ends a task |
| `FocusStatus` | `{ sessionId, status, context, timestamp }` | Classification result from any device |
| `DeviceOnline` | `{ deviceId, deviceType, name }` | Device heartbeat received |
| `DeviceOffline` | `{ deviceId }` | Device heartbeat timeout |
| `PlanChanged` | `{ planType }` | Subscription webhook processed |

### Client → Server Messages

| Message | Payload | Sender |
|---|---|---|
| `SubmitFocusStatus` | `{ sessionId, status, context }` | Desktop app, extension |
| `StartTask` | `{ taskTitle, hints }` | Any client |
| `EndTask` | `{ sessionId }` | Any client |

### Coexistence with Local WebSocket

- The local WebSocket (`ws://localhost:9876/focusbot`) **remains active** for same-machine desktop ↔ extension communication (low latency, works offline).
- SignalR is used **in addition** for cross-machine sync when the user is signed in with a cloud plan.
- Clients must handle both channels without duplicate processing (deduplicate by event ID or session ID + timestamp).

### Tasks

- [ ] Add SignalR to WebAPI (`Microsoft.AspNetCore.SignalR`)
- [ ] Define hub interface and message DTOs
- [ ] Implement authentication for SignalR (Supabase JWT)
- [ ] Add SignalR client package to Windows app
- [ ] Add SignalR client to browser extension (via `@microsoft/signalr` npm package)
- [ ] Define deduplication strategy for local WS + SignalR overlap
- [ ] Document reconnection behavior (exponential backoff, max retries)

---

## 13. Shared Entitlement Rules

### Rule Table

| Action | Free (BYOK) | Cloud BYOK | Cloud Managed |
|---|---|---|---|
| Start local session | Yes | Yes | Yes |
| Classify via client | Yes (BYOK) | Yes (BYOK) | No |
| Classify via backend | No | No | Yes |
| Submit session to cloud | No | Yes | Yes |
| Submit events to cloud | No | Per §7 decision | Per §7 decision |
| View basic analytics | Yes (local) | Yes (local) | Yes (local) |
| View full analytics | No | Yes (web app) | Yes (web app) |
| Register device | No | Yes | Yes |
| Send heartbeat | No | Yes | Yes |
| Connect via SignalR | No | Yes | Yes |
| Access sync | No | Yes | Yes |

### Enforcement

- **Client-side:** Clients check local plan type before showing UI or making calls. Plan type is cached locally after `/auth/me` or `/subscriptions/status` call.
- **Server-side:** Backend validates plan entitlements on all cloud endpoints. Returns `403 Forbidden` for plan-gated endpoints when the user's plan does not include the feature.

### Tasks

- [ ] Add `PlanType` to `User` or `Subscription` entity
- [ ] Add entitlement check middleware or helper for WebAPI endpoints
- [ ] Add plan-aware UI gating in Windows app
- [ ] Add plan-aware UI gating in browser extension
- [ ] Add plan-aware access control in web app

---

## Exit Criteria

- [ ] This document is finalized and reviewed
- [ ] All open design decisions (§7) are resolved
- [ ] All entity schemas are agreed upon
- [ ] All API contracts are documented
- [ ] All clients reference this spec in their implementation epics
- [ ] No client implementation begins without approved contracts
