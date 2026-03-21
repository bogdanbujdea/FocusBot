# Epic 2 — Windows App Local/Basic Mode

> **Implementation note (supersedes original plan):** See `updated-plan-for-epic-2.md` for the actual implementation. Key deviation: **no SignalR** — desktop communicates with the extension via local WebSocket only; cross-device real-time sync is deferred to Epic 6.

## Objective

Extend the existing Windows app with plan awareness, device registration, and cloud session submission. The Windows app remains the primary native experience for desktop capture and live alignment. All classification routes through the backend (`POST /classify`). Cross-machine sync (SignalR) is deferred to Epic 6.

**Depends on:** Epic 1 (Shared Contracts & Entitlement Model)

---

## Current State — What Already Exists

> The Windows app is mature. Most core features are implemented. This epic is **delta work** on top of the existing codebase.

| Feature | Status | Details |
|---|---|---|
| BYOK classification | `[EXISTS]` | Multi-provider via LlmTornado (OpenAI, Anthropic, Google). Provider selection and API key management implemented. |
| Managed classification | `[EXISTS]` | Routes to `POST /classify` when no BYOK key is configured and user is authenticated. |
| Classification caching | `[EXISTS]` | SHA-256 hash-based cache in `AlignmentCacheRepository` (SQLite). No expiry. |
| Supabase auth | `[EXISTS]` | Magic link via `SupabaseAuthService`. Deep link callback (`foqus://auth-callback`). Token stored locally. Refresh via `RefreshTokenAsync()`. |
| Focus Score | `[EXISTS]` | Time-weighted alignment percentage (0–100), computed continuously during a session. |
| Session lifecycle | `[EXISTS]` | Start/end task flow. Session history stored in local SQLite. |
| Daily analytics | `[EXISTS]` | Focused time, distraction count, top distraction apps, avg distraction cost, longest focused session. `IDailyAnalyticsService`, `IDistractionAnalyticsService`. |
| Session history | `[EXISTS]` | Local session list with task name, duration, Focus Score, top 3 distraction apps. |
| Alignment overlay | `[EXISTS]` | Floating circle overlay showing current classification status. |
| Idle detection | `[EXISTS]` | Pauses tracking after 5 minutes of inactivity via Win32 `GetLastInputInfo`. |
| WebSocket integration | `[EXISTS]` | Server on `ws://localhost:9876/focusbot` for desktop ↔ extension shared task sync. |
| Trial service | `[EXISTS]` | `ITrialService` interface for 24-hour trial. |
| Subscription service | `[EXISTS]` | `ISubscriptionService` interface. |
| Foreground monitoring | `[EXISTS]` | Win32 polling every 1s (`GetForegroundWindow`, `GetWindowText`). |

---

## What's New or Changed

### Summary

| Work Item | Type | Priority |
|---|---|---|
| Plan selection UI | `[NEW]` | High |
| Device registration + heartbeat | `[NEW]` | High |
| Cloud session submission | `[NEW]` | High |
| ~~SignalR client for server-mediated sync~~ | ~~`[NEW]`~~ | **Deferred to Epic 6** |
| Plan-aware classification routing | `[CHANGED]` | High |
| Auth flow — plan/entitlement fetch | `[CHANGED]` | High |
| Session.DeviceId attribution | `[CHANGED]` | Medium |
| Basic analytics page — spec alignment | `[CHANGED]` | Medium |
| BYOK flow — UX polish | `[CHANGED]` | Medium |
| "Open full analytics" CTA | `[NEW]` | Medium |
| Alignment overlay — state refinement | `[CHANGED]` | Low |
| Token refresh alignment | `[CHANGED]` | Low |

---

## Detailed Tasks

### 1. Plan Selection UI `[NEW]`

Add a "Choose Plan" screen accessible from:
- Post-login flow (first time)
- Settings page (change plan)

**Display three tiers:**

| Plan | Description |
|---|---|
| Free (BYOK) | Use your own API key. Local basic analytics only. |
| Cloud BYOK | Use your own API key + full analytics, sync, and integrations in the web app. |
| Cloud Managed | We provide the API key. Full analytics, sync, and integrations in the web app. |

**Behavior:**
- Free is the default for unauthenticated users.
- After login, fetch plan from `/subscriptions/status` (or `/auth/me` if plan is included).
- If user selects a paid plan, open Paddle Checkout in the default browser.
- After successful payment, backend processes Paddle webhook and updates subscription.
- App polls `/subscriptions/status` every 5 minutes for plan updates. (SignalR `PlanChanged` deferred to Epic 6.)

**UI elements:**
- Plan comparison cards with feature highlights
- Current plan indicator
- "Upgrade" / "Change plan" button
- Link to manage billing in web app

### 2. Device Registration + Heartbeat `[NEW]`

When the user is signed in with a cloud plan:

**Registration (on login or first cloud-plan activation):**
- Generate a stable device fingerprint (GUID persisted in local settings, survives app updates).
- Call `POST /devices` with:
  - `deviceType`: `Desktop`
  - `name`: Machine hostname or user-configurable name
  - `fingerprint`: The stable GUID
  - `appVersion`: Current app version
  - `platform`: Windows version string
- Store the returned `deviceId` locally.

**Heartbeat (while app is running and signed in):**
- Send `PUT /devices/{deviceId}/heartbeat` every 60 seconds.
- Include current `appVersion` and `platform`.
- Handle 401 (token expired → refresh) and 404 (device deleted → re-register).

**Deregistration:**
- On explicit logout, optionally call `DELETE /devices/{deviceId}`.
- On uninstall, no action needed (backend marks offline after heartbeat timeout).

### 3. Cloud Session Submission `[NEW]`

When signed in with a cloud plan (Cloud BYOK or Cloud Managed):

**Session start:**
- Call `POST /sessions` with `deviceId` in the request body.
- Store the server-side `sessionId` alongside the local session.

**During session (if Epic 1 §7 resolves to Option B — events):**
- Submit classification events to `POST /sessions/{id}/events` in near-real-time or batched.
- Queue events locally if offline, replay on reconnect.
- Use client-generated `eventId` UUIDs for idempotency.

**Session end:**
- Call `POST /sessions/{id}/end` with the session summary payload (per Epic 1 §8 schema).
- Include `deviceId` in the payload.
- Retry on failure with exponential backoff.

**Offline handling:**
- If the network is unavailable, store session data locally.
- Submit on next successful connection.
- Do not block the user from starting/ending sessions locally.

### 4. ~~SignalR Client for Server-Mediated Sync~~ `[DEFERRED — Epic 6]`

**Decision:** No SignalR in Epic 2. Desktop communicates with the browser extension via the existing local WebSocket server only. Cross-machine real-time sync (task start/end broadcast, `PlanChanged` push, device presence) is deferred to Epic 6.

**Reason:** Keeps the MVP desktop thin. Local WebSocket covers 100% of the same-machine use case. Cross-machine sync adds implementation complexity that is better addressed once the web app dashboard and multi-device data model are stable.

**What ships in Epic 2 instead:** Plan changes are detected by polling `GET /subscriptions/status` every 5 minutes while the app is running.

### 5. Plan-Aware Classification Routing `[CHANGED]`

Update the existing classification routing to be plan-aware:

| Plan | Route |
|---|---|
| Free (BYOK) | Client → LLM provider directly (existing behavior) |
| Cloud BYOK | Client → LLM provider directly (existing behavior) + submit results to cloud |
| Cloud Managed | Client → `POST /classify` (existing behavior) |

**Implementation:**
- Add a `ClassificationRouteResolver` (or extend existing routing logic) that reads the current plan and key configuration.
- The UI and `FocusSessionService` should not know which route is active — only the resolver decides.
- For Cloud BYOK, after local classification, fire-and-forget the result to the cloud (per §3).

### 6. Auth Flow — Plan/Entitlement Fetch `[CHANGED]`

After successful login:

1. Call `/auth/me` or `/subscriptions/status` to get the user's plan type.
2. Cache plan type locally (survives app restarts).
3. Gate features based on plan:
   - Free: no device registration, no cloud sync, no full analytics link active.
   - Cloud: enable device registration, cloud session submission, full analytics CTA.
4. Handle plan changes:
   - Poll `/subscriptions/status` every 5 minutes while the app is running.
   - No SignalR push in this epic (deferred to Epic 6).

**Token refresh alignment:**
- `RefreshTokenAsync()` is called proactively when token has < 5 minutes remaining.
- On refresh failure: retry 3 times with exponential backoff, then fire `ReAuthRequired` event and navigate to Settings.

### 7. Session.DeviceId Attribution `[CHANGED]`

- When creating sessions via `POST /sessions`, include `deviceId` in the request.
- When ending sessions via `POST /sessions/{id}/end`, include `deviceId` in the payload.
- Local-only sessions (free plan) do not need `deviceId`.

### 8. Basic Analytics Page — Spec Alignment `[CHANGED]`

Audit the existing daily analytics against Epic 1 §2 ("Basic Analytics Specification"):

**Already covered:**
- Session list with task name, duration, Focus Score ✓
- Focused time / distracted time ✓
- Distraction count ✓
- Top distraction apps ✓

**Potentially missing or needing adjustment:**
- "Last 7 days summary" as a distinct view (daily totals for the past week)
- Consistency with the canonical field names from Epic 1

**New:**
- Add a visible "Open full analytics" button/link that opens `app.foqus.me/analytics` in the default browser.
- Show the button only when signed in with a cloud plan.
- For free users, show a softer CTA: "Upgrade for full analytics →"

### 9. BYOK Flow — UX Polish `[CHANGED]`

The existing BYOK flow works but may need polish:

- Confirm provider selection UI is clear (OpenAI / Anthropic / Google dropdown or cards).
- Add key validation — call the provider with a lightweight test request on save.
- Show clear status indicators:
  - ✓ Key configured and valid
  - ✗ Invalid key
  - ⚠ Provider unavailable
- Handle key removal (switch to free plan or managed plan).

### 10. Alignment Overlay — State Refinement `[CHANGED]`

The floating circle overlay exists. Review and ensure it handles:

- `Loading` state (classification in progress)
- `Error` state (provider error, network error)
- `Unknown` state (no task active, or first classification pending)
- Tooltip with brief explanation of the current status
- Plan-aware messaging (e.g., "Classification paused — upgrade to continue" if trial expired on managed plan)

### 11. Local Storage Model `[UNCHANGED]`

SQLite remains the local storage:
- Sessions, classifications, analytics events — for basic analytics.
- Settings: provider, API key presence, plan mode, overlay preferences, auth tokens.
- Device fingerprint and `deviceId` (new fields in settings).

No migration to a different local storage engine is needed.

---

## Technical Notes

- All new services should follow the existing Clean Architecture pattern: interfaces in Core, implementations in Infrastructure.
- Use `CSharpFunctionalExtensions.Result<T>` for service returns.
- Use primary constructors for DI.
- Async methods must accept `CancellationToken`.
- `TreatWarningsAsErrors` is on — no nullable warnings.
- **No SignalR in this epic.** Cross-device sync deferred to Epic 6.

---

## Exit Criteria

- [ ] A user can install the Windows app and use BYOK locally without signing in
- [ ] A user can sign in via magic link
- [ ] After login, the user can choose a plan (Free / Cloud BYOK / Cloud Managed)
- [ ] Free users see local basic analytics and an upgrade CTA
- [ ] Cloud users' sessions are submitted to the backend with device attribution
- [ ] Cloud Managed users' classifications route through the backend
- [ ] Cloud BYOK users classify locally and submit results to the cloud
- [ ] The app registers as a device and sends heartbeats when signed in
- [ ] ~~The app connects to SignalR for cross-device sync when on a cloud plan~~ (deferred to Epic 6)
- [ ] The local WebSocket still works for same-machine extension communication
- [ ] "Open full analytics" navigates to the web app
- [ ] The app appears in the integrations page with online/offline status
