# Epic 5 — Web Full Analytics

## Objective

Build the centralized full analytics dashboard in the web app (`app.foqus.me`) for paid cloud users. This is the **single place** for advanced analytics — native clients (Windows app, browser extension) intentionally do not duplicate this UI.

**Depends on:** Epic 1 (Contracts), Epic 3 (Web App Platform), Epic 2 (Windows backend integration — session sync, devices, plan types), and/or Epic 4 (extension) for data-producing clients.

---

## Alignment with Epic 2 (revised plan)

These assumptions are **locked** for analytics scope (see `docs/MVP/epics/epic-2-windows-app/updated-plan-for-epic-2.md`):

| Topic | Decision | Implication for Epic 5 |
|---|---|---|
| Server-side event log | **No** per-classification events stored in PostgreSQL | No per-minute heatmaps, no “timeline of every classify call” in the web app unless a future epic adds event storage. |
| Session payload | **Enriched summary at session end** (scalars + top apps computed locally) | Aggregations are **session-granular** (per day/week from completed sessions). |
| `POST /classify` | Stateless (cache only) | Charts derive from **synced sessions**, not from classify history. |
| Devices | `DeviceType`: **Desktop**, **Extension** only (no “Web” device) | Device breakdown uses `Session.DeviceId` → `Devices` table. |
| Plans | `FreeBYOK`, `CloudBYOK`, `CloudManaged` | Full analytics is a **cloud** feature (both paid cloud tiers); free BYOK uses local analytics only. |
| Real-time sync | **No SignalR** in MVP | Web dashboard is **pull-based** (HTTP); no live “session in progress” streaming requirement for this epic. |

---

## Current State (codebase)

| Component | Status | Details |
|---|---|---|
| **`Session` entity (PostgreSQL)** | `[EXISTS]` | `UserId`, optional `DeviceId`, `SessionTitle`, `Context`, `StartedAtUtc`, `EndedAtUtc`, pause fields (`PausedAtUtc`, `TotalPausedSeconds`, `IsPaused`), summary scalars: `FocusScorePercent`, `FocusedSeconds`, `DistractedSeconds`, `DistractionCount`, `ContextSwitchCount`, `Source`. **No** `TopDistractingApps` / `TopAlignedApps` columns (removed in migration `RemovedColumns`); clients still send these in `EndSessionPayload` but the Web API **does not** bind or persist them today. |
| **`Device` entity + `/devices`** | `[EXISTS]` | Registration, list, heartbeat, delete — see `Features/Devices/`. |
| **`/sessions` API** | `[PARTIAL]` | Implemented: `POST /`, `POST /{id}/end`, `POST /{id}/pause`, `POST /{id}/resume`, `GET /active`, `GET /` (pagination: `page`, `pageSize` only), `GET /{id}`. **Not yet:** date/device/task filters, sort options, or device name/type on list items. |
| **Backend analytics aggregation** | `[NEW]` | No `Features/Analytics/` slice; no `/analytics/*` routes. |
| **Web analytics UI** | `[NEW]` | `foqus-web-app` `AnalyticsPage` is still a “coming soon” placeholder. `DashboardPage` shows a static “recent sessions” empty state — not wired to the API yet. |
| **Plan / subscription model** | `[EXISTS]` | `Subscription.PlanType` + `GET /subscriptions/status` (and auth/me) — use for backend and web gating. |
| **“Open / View full analytics”** | `[PARTIAL]` | Windows: **Plan & billing** flow (`PlanSelectionViewModel`) opens `https://app.foqus.me/analytics` for cloud plans with upgrade messaging for free. Browser extension: **Options** page includes “View full analytics →” to the same URL. Epic 5 still owns making the **destination** useful. |

---

## Deliverables

1. Backend `Analytics` feature slice with aggregation endpoints **or** a documented choice to implement aggregations entirely in the web app over `GET /sessions` once filtering exists (prefer a dedicated slice for performance and clear plan-gating).
2. Extended `GET /sessions` (and/or `GET /sessions/{id}`) with filtering, sorting, and **device enrichment** (`deviceType`, `deviceName` from `Device`).
3. **Persist or otherwise surface top-app data** if “Top distractions / Top aligned” remain in scope (see [Top apps gap](#top-apps-gap)).
4. Web analytics dashboard UI under `/analytics`.
5. Plan-aware access control (cloud only) on the backend; upgrade UX for free users on the web app.
6. Keep navigation from native clients accurate as the dashboard ships (links already point at `/analytics`).

---

## Detailed Tasks

### 1. Backend — Analytics Feature Slice `[NEW]`

Create: `src/FocusBot.WebAPI/Features/Analytics/`

#### Files (suggested)

```
Features/Analytics/
├── SLICE.md
├── AnalyticsEndpoints.cs
├── AnalyticsService.cs
├── AnalyticsDtos.cs
└── AnalyticsQueryParameters.cs
```

#### Endpoints

| Method | Path | Auth | Plan Gate | Description |
|---|---|---|---|---|
| `GET /analytics/summary` | Yes | Cloud only | Aggregated metrics for a date range |
| `GET /analytics/trends` | Yes | Cloud only | Time-series data for charts |
| `GET /analytics/devices` | Yes | Cloud only | Per-device breakdown |
| `GET /analytics/top-distractions` | Yes | Cloud only | Ranked distracting apps/sites |
| `GET /analytics/top-aligned` | Yes | Cloud only | Ranked aligned apps/sites |

#### `GET /analytics/summary`

**Query parameters:**

| Parameter | Type | Required | Default |
|---|---|---|---|
| `from` | DateTimeOffset | No | 7 days ago |
| `to` | DateTimeOffset | No | Now |
| `deviceId` | Guid | No | All devices |

**Response (example):**
```json
{
  "period": { "from": "2026-03-13T00:00:00Z", "to": "2026-03-20T00:00:00Z" },
  "totalSessions": 42,
  "totalFocusedSeconds": 86400,
  "totalDistractedSeconds": 21600,
  "averageFocusScorePercent": 78,
  "totalDistractionCount": 156,
  "totalContextSwitchCount": 120,
  "averageSessionDurationSeconds": 2571,
  "longestSessionSeconds": 7200,
  "devicesActive": 2
}
```

> **Note:** The stored field is **`ContextSwitchCount`** (integer count from the client tracker), not “context switch cost seconds.” Name DTOs to match the domain.

#### `GET /analytics/trends`

**Query parameters:**

| Parameter | Type | Required | Default |
|---|---|---|---|
| `from` | DateTimeOffset | No | 30 days ago |
| `to` | DateTimeOffset | No | Now |
| `granularity` | enum (daily, weekly, monthly) | No | daily |
| `deviceId` | Guid | No | All devices |

**Response:**
```json
{
  "granularity": "daily",
  "dataPoints": [
    {
      "date": "2026-03-13",
      "sessions": 6,
      "focusedSeconds": 12000,
      "distractedSeconds": 3000,
      "focusScorePercent": 80,
      "distractionCount": 22
    }
  ]
}
```

#### `GET /analytics/devices`

**Query parameters:** `from`, `to` (same as summary).

**Response:**
```json
{
  "devices": [
    {
      "deviceId": "uuid",
      "deviceType": "Desktop",
      "name": "Work Laptop",
      "sessions": 28,
      "focusedSeconds": 60000,
      "distractedSeconds": 15000,
      "focusScorePercent": 80
    }
  ]
}
```

#### `GET /analytics/top-distractions` / `GET /analytics/top-aligned`

**Query parameters:** `from`, `to`, `deviceId`, `limit` (default 10).

**Response:**
```json
{
  "items": [
    { "name": "twitter.com", "occurrences": 45, "totalSeconds": 2700 },
    { "name": "reddit.com", "occurrences": 32, "totalSeconds": 1920 }
  ]
}
```

#### Top apps gap

**Today:** PostgreSQL `Session` rows **do not** store top-app JSON. The Windows client’s `EndSessionPayload` still includes `TopDistractingApps` and `TopAlignedApps`, but `EndSessionRequest` / `SessionService` ignore them.

**Options for Epic 5 (pick one in implementation planning):**

1. **Re-add nullable JSON/text columns** on `Session` (e.g. `TopDistractingApps`, `TopAlignedApps`) and extend `EndSessionRequest` + `EndSessionAsync` to persist them — enables `/analytics/top-*` from SQL.
2. **Omit server-side top-app endpoints** until storage exists; ship summary + trend charts from scalars only, and show “top apps” only in clients that still hold local history.

Without (1) or another store, **top-distractions / top-aligned in the web dashboard cannot be faithful to cloud history.**

#### Aggregation strategy

- **Primary source:** completed rows in `Session` joined to `Device` where needed.
- **Computed on read** for MVP (no materialized views) is consistent with Epic 2’s session-only model.
- **Indexes:** today only a partial unique index on `UserId` for active sessions exists. Add composite indexes for analytics, e.g. `(UserId, StartedAtUtc)` and `(UserId, DeviceId, StartedAtUtc)` for filtered range queries.

**Out of scope unless a future epic adds event tables:** drill-down to individual classification timestamps server-side.

### 2. Extended Sessions Endpoints `[CHANGED]`

Enhance **`GET /sessions`** (and optionally single-session responses):

**New query parameters:**

| Parameter | Type | Description |
|---|---|---|
| `deviceId` | Guid | Filter by device |
| `from` | DateTimeOffset | Sessions **started** on or after (define inclusive/exclusive in API contract) |
| `to` | DateTimeOffset | Sessions **started** on or before |
| `taskTitle` | string | Filter by session title (contains) — maps to `SessionTitle` |
| `sortBy` | enum | `startedAt` (default), `focusScore`, `duration` |
| `sortOrder` | enum | `desc` (default), `asc` |

**Response enhancement:**

- Include `deviceType` and `deviceName` per session (join `Device` on `Session.DeviceId`).

> **Existing behavior to preserve in docs:** pause/resume endpoints and `SessionResponse` pause fields are already part of the API; the web session history should display paused time / state consistently if shown.

### 3. Web Analytics Dashboard UI

Build the analytics pages under `/analytics` in `foqus-web-app`.

#### Page Structure

```
/analytics
├── Summary Cards (top row)
│   ├── Total Focused Time
│   ├── Total Distracted Time
│   ├── Average Focus Score
│   ├── Total Sessions
│   └── Total Distractions / context switches (label to match stored semantics)
├── Date Range Picker + Device Filter
├── Trend Chart (focus vs distraction over time)
├── Device Breakdown (bar/pie chart)
├── By-Task Breakdown (table or chart) — group by SessionTitle
├── Top Distractions (ranked list) — only if backend stores or aggregates app-level data
├── Top Aligned Activities (ranked list) — same caveat
├── Session History (paginated table)
│   ├── Task / session title
│   ├── Date/time
│   ├── Duration
│   ├── Focus Score
│   ├── Device
│   └── [View details]
└── Period Comparison (this week vs last week)
```

#### Components

| Component | Description |
|---|---|
| `AnalyticsSummaryCards` | Key metrics in card layout |
| `DateRangePicker` | From/to selector with presets (Today, 7d, 30d, 90d, Custom) |
| `DeviceFilter` | Dropdown populated from `GET /devices` (or equivalent), or from sessions if devices endpoint is not used client-side |
| `TrendChart` | Line/area chart — focused vs distracted seconds over time |
| `DeviceBreakdownChart` | Bar or pie chart — metrics per device |
| `TaskBreakdownTable` | Table grouping sessions by `SessionTitle` with aggregate metrics |
| `TopDistractionsTable` | Ranked list — **conditional on data availability** |
| `TopAlignedTable` | Ranked list — **conditional on data availability** |
| `SessionHistoryTable` | Paginated, sortable, filterable session list |
| `PeriodComparison` | Side-by-side comparison of two periods |

#### Charting Library

**Design decision:** Choose one before implementation.

Options:

- **Recharts** — React-native, composable, good for dashboards
- **Chart.js + react-chartjs-2** — Widely used, lightweight
- **Tremor** — React dashboard components, Tailwind-based, includes charts + cards
- **Nivo** — D3-based, rich chart types, React-native

**Recommendation:** Tremor if using Tailwind, otherwise Recharts.

#### Empty States

- No data yet: "Start a focus session in the Windows app or browser extension to see your analytics here."
- No data for selected filters: "No sessions found for the selected date range and device."

**Reference:** The browser extension already implements **local** session-level analytics (`browser-extension` analytics page) using a similar mental model (totals by day, KPIs) — useful UX reference, not a dependency.

### 4. Access Control

#### Plan Gating — Backend

All `/analytics/*` endpoints require authentication AND a **cloud** plan (`CloudBYOK` or `CloudManaged`):

```csharp
// Pseudocode for plan check
if (user.PlanType == PlanType.FreeBYOK)
    return Results.StatusCode(403, "Upgrade to a cloud plan for full analytics");
```

Return `403 Forbidden` with a machine-readable error code (`plan_required`) and human-readable message.

#### Plan Gating — Web App

- Resolve plan from the same source as the rest of `foqus-web-app` (subscription status / `auth/me` once exposed to the client).
- If free BYOK: show the analytics page shell with an **upgrade overlay**:
  - "Full analytics require a cloud plan"
  - Plan comparison card
  - "Upgrade now" button → Paddle checkout (existing checkout routes as elsewhere in the app)

### 5. Navigation from Native Clients

#### Windows App

- **Current:** Plan/billing UI opens full analytics for cloud users (`https://app.foqus.me/analytics`); free users see upgrade-oriented copy.
- **Optional enhancement:** duplicate a lightweight link from **History** if product wants parity with the original epic note — not required if settings/plan flow is the single entry point.

#### Browser Extension

- **Current:** Options page includes "View full analytics →" to `https://app.foqus.me/analytics`.
- Epic 5 ensures the destination implements the dashboard and plan-aware empty states.

---

## Data Requirements

### Minimum Viable Analytics

1. **Synced completed sessions** from desktop and/or extension (cloud plans).
2. **Device attribution** where possible (`Session.DeviceId`); older rows may have `null` device id.
3. **Sufficient history** — analytics are most valuable with 7+ days of data.
4. **Top-app lists (optional MVP slice):** require **persisting** client-provided top-app summaries or dropping those widgets until storage exists.

### Data Volume Estimates

| Metric | Estimate |
|---|---|
| Sessions per user per day | 3–10 |
| Session size (DB row) | ~500 bytes (+ JSON if top apps restored) |
| 30-day storage per user (sessions only) | ~150 KB |

**Implication:** Computed-on-read from `Session` only remains feasible for typical users. If top-app JSON is added, keep payload bounded (client already serializes ranked lists).

---

## Technical Notes

- Follow vertical slice architecture for the Analytics feature slice.
- Use `Result<T>` pattern for service returns (match existing WebAPI slices).
- Add database indexes for analytics query performance (see [Aggregation strategy](#aggregation-strategy)).
- Consider query caching for frequently accessed date ranges (LRU or time-based).
- Web app charts should be responsive and work on tablet viewports.
- **Naming:** Prefer `SessionTitle` / “session title” in user-facing copy; internal docs may still say “task” where it maps to the same field.

---

## Exit Criteria

- [ ] Backend `Analytics` feature slice is implemented with all 5 endpoints **or** an explicit ADR documents server-side vs client-side aggregation with equivalent UX.
- [ ] **If** top-app widgets are in scope: `Session` (or equivalent) persists top distracting/aligned data and `/analytics/top-*` (or client aggregation) uses it.
- [ ] `GET /sessions` supports filtering by device, date range, title, and sorting; responses include device metadata where `DeviceId` is set.
- [ ] Analytics dashboard renders summary, trends, breakdowns, and session history from real API data.
- [ ] Date range picker and device filter work correctly.
- [ ] Free BYOK users see an upgrade CTA instead of analytics data (web + API).
- [ ] Cloud BYOK and Cloud Managed users see full analytics.
- [ ] Windows app entry point(s) to `app.foqus.me/analytics` remain correct for cloud vs free.
- [ ] Extension “View full analytics” remains correct.
- [ ] Empty states are handled gracefully.
- [ ] Analytics respond within acceptable latency (< 2s target for a 30-day range on typical data).
