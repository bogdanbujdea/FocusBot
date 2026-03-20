# Epic 5 — Web Full Analytics

## Objective

Build the centralized full analytics dashboard in the web app (`app.foqus.me`) for paid cloud users. This is the **single place** for advanced analytics — native clients (Windows app, extension) intentionally do not duplicate this UI.

**Depends on:** Epic 1 (Contracts), Epic 3 (Web App Platform), and at least one data-producing client (Epic 2 or Epic 4)

---

## Current State

| Component | Status | Details |
|---|---|---|
| Backend session storage | `[EXISTS]` | `Session` entity stores `FocusScorePercent`, `FocusedSeconds`, `DistractedSeconds`, `DistractionCount`, `ContextSwitchCostSeconds`, `TopDistractingApps`. |
| Backend analytics aggregation | `[NEW]` | No aggregation endpoints. Raw session data exists but is not summarized or queryable for analytics. |
| Web analytics UI | `[NEW]` | Analytics page is a placeholder (from Epic 3). |
| Session event storage | `[DEPENDS]` | Depends on Epic 1 §7 decision (Option A: summaries only, Option B: events + summaries). |
| `GET /sessions` endpoint | `[EXISTS]` | Paginated session history. Needs filter extensions. |

---

## Deliverables

1. Backend `Analytics` feature slice with aggregation endpoints
2. Extended `Sessions` endpoints with filtering
3. Web analytics dashboard UI
4. Plan-aware access control (paid only)
5. Upgrade prompts for free users
6. Navigation integration from native clients

---

## Detailed Tasks

### 1. Backend — Analytics Feature Slice `[NEW]`

Create: `src/FocusBot.WebAPI/Features/Analytics/`

#### Files

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

**Response:**
```json
{
  "period": { "from": "2026-03-13T00:00:00Z", "to": "2026-03-20T00:00:00Z" },
  "totalSessions": 42,
  "totalFocusedSeconds": 86400,
  "totalDistractedSeconds": 21600,
  "averageFocusScorePercent": 78,
  "totalDistractionCount": 156,
  "totalContextSwitchCostSeconds": 3120,
  "averageSessionDurationSeconds": 2571,
  "longestSessionSeconds": 7200,
  "devicesActive": 2
}
```

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

#### `GET /analytics/top-distractions`

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

#### `GET /analytics/top-aligned`

Same shape as top-distractions, but for aligned apps/sites.

#### Aggregation Strategy

**Option A (if only session summaries are synced):**
- Aggregate directly from the `Session` table.
- `TopDistractingApps` is a JSON column — parse and aggregate across sessions.
- Computed on read (no materialized views initially).
- Add database indexes on `(UserId, StartedAtUtc)` and `(UserId, DeviceId, StartedAtUtc)`.

**Option B (if events are synced):**
- Aggregate from the `SessionEvent` table for granular per-event analytics.
- Materialized views or periodic aggregation jobs for performance.
- Enables timeline drill-down (what was I doing at 2:15 PM?).

**Recommendation:** Start with computed-on-read from the `Session` table (Option A). Add materialized aggregation later if performance requires it.

### 2. Extended Sessions Endpoints `[CHANGED]`

Enhance the existing `GET /sessions` endpoint:

**New query parameters:**

| Parameter | Type | Description |
|---|---|---|
| `deviceId` | Guid | Filter by device |
| `from` | DateTimeOffset | Sessions started after this time |
| `to` | DateTimeOffset | Sessions started before this time |
| `taskTitle` | string | Search/filter by task name (contains) |
| `sortBy` | enum | `startedAt` (default), `focusScore`, `duration` |
| `sortOrder` | enum | `desc` (default), `asc` |

**Response enhancement:**
- Include `deviceType` and `deviceName` in each session response DTO (joined from `Device` table).

### 3. Web Analytics Dashboard UI

Build the analytics pages under `/analytics` in the web app.

#### Page Structure

```
/analytics
├── Summary Cards (top row)
│   ├── Total Focused Time
│   ├── Total Distracted Time
│   ├── Average Focus Score
│   ├── Total Sessions
│   └── Total Distractions
├── Date Range Picker + Device Filter
├── Trend Chart (focus vs distraction over time)
├── Device Breakdown (bar/pie chart)
├── By-Task Breakdown (table or chart)
├── Top Distractions (ranked list)
├── Top Aligned Activities (ranked list)
├── Session History (paginated table)
│   ├── Task name
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
| `DeviceFilter` | Dropdown to filter by device or "All devices" |
| `TrendChart` | Line/area chart — focused vs distracted seconds over time |
| `DeviceBreakdownChart` | Bar or pie chart — metrics per device |
| `TaskBreakdownTable` | Table grouping sessions by task name with aggregate metrics |
| `TopDistractionsTable` | Ranked list of most distracting apps/sites |
| `TopAlignedTable` | Ranked list of most aligned apps/sites |
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

### 4. Access Control

#### Plan Gating — Backend

All `/analytics/*` endpoints require authentication AND a cloud plan:

```csharp
// Pseudocode for plan check
if (user.PlanType == PlanType.Free)
    return Results.StatusCode(403, "Upgrade to a cloud plan for full analytics");
```

Return `403 Forbidden` with a machine-readable error code (`plan_required`) and human-readable message.

#### Plan Gating — Web App

- Check plan type from stored auth/subscription status.
- If free: show the analytics page shell with an **upgrade overlay**:
  - "Full analytics require a cloud plan"
  - Plan comparison card
  - "Upgrade now" button → Paddle checkout

### 5. Navigation from Native Clients

#### Windows App

- "Open full analytics" button on the basic analytics page.
- Launches `https://app.foqus.me/analytics` in the default browser.
- Only visible when signed in with a cloud plan.
- For free users: "Upgrade for full analytics →" as a softer CTA.

#### Browser Extension

- "View full analytics →" link in popup and options page.
- Opens `https://app.foqus.me/analytics` in a new tab.
- Same plan-aware visibility rules.

> **Note:** The navigation links may already be added in Epics 2 and 4. This epic ensures the destination page is functional.

---

## Data Requirements

### Minimum Viable Analytics

For the analytics dashboard to be useful, it needs:

1. **Synced sessions** from at least one client (Epic 2 or 4 cloud session submission).
2. **Device attribution** on sessions (`Session.DeviceId` from Epic 1 §4).
3. **Sufficient history** — analytics are most valuable with 7+ days of data.

### Data Volume Estimates

| Metric | Estimate |
|---|---|
| Sessions per user per day | 3–10 |
| Session size (DB row) | ~500 bytes |
| Events per session (if Option B) | 50–200 |
| Event size (DB row) | ~300 bytes |
| 30-day storage per user (sessions only) | ~150 KB |
| 30-day storage per user (events) | ~6 MB |

**Implication:** Computed-on-read is feasible for sessions-only aggregation. If events are stored, consider periodic aggregation for users with high activity.

---

## Technical Notes

- Follow vertical slice architecture for the Analytics feature slice.
- Use `Result<T>` pattern for service returns.
- Add database indexes for analytics query performance.
- Consider query caching for frequently accessed date ranges (LRU or time-based).
- Web app charts should be responsive and work on tablet viewports.

---

## Exit Criteria

- [ ] Backend `Analytics` feature slice is implemented with all 5 endpoints
- [ ] `GET /sessions` supports filtering by device, date range, task, and sorting
- [ ] Analytics dashboard renders summary, trends, breakdowns, and session history
- [ ] Date range picker and device filter work correctly
- [ ] Free users see an upgrade CTA instead of analytics data
- [ ] Cloud BYOK and Cloud Managed users see full analytics
- [ ] Windows app "Open full analytics" navigates to the web dashboard
- [ ] Extension "View full analytics" navigates to the web dashboard
- [ ] Empty states are handled gracefully
- [ ] Analytics respond within acceptable latency (< 2s for 30-day range)
