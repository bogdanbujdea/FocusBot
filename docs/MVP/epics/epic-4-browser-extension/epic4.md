# Epic 4 — Browser Extension Local/Basic Mode

## Objective

Extend the existing browser extension with plan awareness, device registration, cloud session submission, and SignalR-based server-mediated sync. The extension provides browser-specific live feedback and local basic analytics, while full analytics live in the web app.

**Depends on:** Epic 1 (Shared Contracts), Epic 3 (Web App — so integrations page and device backend exist)

---

## Current State — What Already Exists

> The extension is feature-rich. Most core features are implemented. This epic is **delta work**.

| Feature | Status | Details |
|---|---|---|
| BYOK classification (OpenAI) | `[EXISTS]` | Direct OpenAI calls from the background service worker. **Single provider only.** |
| Managed classification | `[EXISTS]` | Routes to `POST /classify` via `apiClient.ts` when in Foqus account mode. |
| Classification caching | `[EXISTS]` | SHA-256 hash-based cache in IndexedDB. |
| Supabase auth | `[EXISTS]` | `signInWithOtp` via Supabase JS client. Options page callback. Token in `chrome.storage.local`. |
| Session lifecycle | `[EXISTS]` | Start/end session. State machine in background service worker. |
| Session summaries | `[EXISTS]` | Focus %, aligned/distracting seconds, distraction count, context switch cost, top domains. |
| Daily analytics | `[EXISTS]` | Aggregations for today/7d/30d in `chrome.storage.local`. |
| Distraction overlay | `[EXISTS]` | Content script with pulse animation, dismissible banner on distracting pages. |
| Popup indicator | `[EXISTS]` | Popup shows current session status, task info, alignment state. |
| Idle pause/resume | `[EXISTS]` | Pauses on system idle, resumes on activity. |
| Excluded domains | `[EXISTS]` | Configurable list of always-aligned domains. |
| WebSocket client | `[EXISTS]` | Connects to `ws://localhost:9876/focusbot` for desktop app shared task sync. |
| Page visit lifecycle | `[EXISTS]` | Tracks classifying → classified → error states per tab. |

---

## What's New or Changed

### Summary

| Work Item | Type | Priority |
|---|---|---|
| Plan selection UI | `[NEW]` | High |
| Device registration + heartbeat | `[NEW]` | High |
| Cloud session submission | `[NEW]` | High |
| SignalR client for server-mediated sync | `[NEW]` | High |
| Plan-aware classification routing | `[CHANGED]` | High |
| Auth flow — plan/entitlement fetch | `[CHANGED]` | High |
| "View full analytics" link | `[NEW]` | Medium |
| Basic analytics — spec alignment | `[CHANGED]` | Medium |
| Multi-provider BYOK | `[OPEN QUESTION]` | Medium |
| Token refresh alignment | `[CHANGED]` | Low |

---

## Detailed Tasks

### 1. Plan Selection UI `[NEW]`

Add plan selection in the options page (and optionally a summary in the popup):

**Options page — Plan section:**

| Plan | Description |
|---|---|
| Free (BYOK) | Use your own API key. Local basic analytics only. |
| Cloud BYOK | Use your own API key + full analytics, sync, and integrations in the web app. |
| Cloud Managed | We provide the API key. Full analytics, sync, and integrations in the web app. |

**Behavior:**
- Free is the default for unauthenticated users.
- After login, fetch plan from `/subscriptions/status` or `/auth/me`.
- If user selects a paid plan, open Paddle Checkout in a new tab.
- After payment, backend processes webhook → extension polls or receives `PlanChanged` via SignalR.

**Popup — Plan awareness:**
- Show current plan badge (Free / Cloud BYOK / Cloud Managed).
- If free, show subtle upgrade CTA.
- Keep messaging consistent with the Windows app (Epic 2).

### 2. Device Registration + Heartbeat `[NEW]`

When signed in with a cloud plan:

**Registration (on login or cloud plan activation):**
- Generate a stable device fingerprint (GUID in `chrome.storage.local`, survives extension updates).
- Call `POST /devices` with:
  - `deviceType`: `Extension`
  - `name`: Browser name (e.g., "Chrome", "Edge") — derived from `navigator.userAgent` or `chrome.runtime.getManifest()`
  - `fingerprint`: The stable GUID
  - `appVersion`: Extension version from `chrome.runtime.getManifest().version`
  - `platform`: Browser + OS string
- Store returned `deviceId` in `chrome.storage.local`.

**Heartbeat:**
- Send `PUT /devices/{deviceId}/heartbeat` every 60 seconds from the background service worker.
- **Service worker keep-alive:** Use chrome alarms API (`chrome.alarms.create`) to wake the service worker for heartbeat. Service workers in Manifest V3 can be terminated after ~30 seconds of inactivity.
- Handle 401 (refresh token) and 404 (re-register).

**Deregistration:**
- On explicit logout, optionally call `DELETE /devices/{deviceId}`.

### 3. Cloud Session Submission `[NEW]`

When signed in with a cloud plan:

**Session start:**
- Call `POST /sessions` with `deviceId`.
- Store server-side `sessionId` in `chrome.storage.local` alongside local session state.

**During session (if Epic 1 §7 resolves to Option B — events):**
- Submit classification events via `POST /sessions/{id}/events`.
- Batch events if the service worker is about to be terminated (save to `chrome.storage.local`, flush on next wake).

**Session end:**
- Call `POST /sessions/{id}/end` with session summary (per Epic 1 §8 schema).
- Include `deviceId`.
- Retry on failure.

**Offline handling:**
- Queue session data in `chrome.storage.local` if network is unavailable.
- Flush queue when connection is restored.

### 4. SignalR Client for Server-Mediated Sync `[NEW]`

When signed in with a cloud plan:

**Connection:**
- Use `@microsoft/signalr` npm package.
- Connect to `/hubs/focus` with Supabase JWT bearer token.
- Maintain connection from the background service worker.
- **Service worker lifecycle:** Use `chrome.alarms` to periodically wake and reconnect if the service worker was terminated. SignalR's auto-reconnect will handle short disconnections; alarms handle service worker restarts.

**Inbound messages (server → client):**
- `TaskStarted`: Another device started a task → update extension state, optionally show notification.
- `TaskEnded`: Another device ended a task → update extension state.
- `FocusStatus`: Classification from another device → update badge/popup if relevant.
- `PlanChanged`: Refresh plan locally.

**Outbound messages (client → server):**
- `SubmitFocusStatus`: Broadcast classification results.
- `StartTask` / `EndTask`: Notify other devices.

**Coexistence with local WebSocket:**
- The local WebSocket client (`ws://localhost:9876/focusbot`) **remains active** for same-machine desktop app communication.
- SignalR is used **in addition** for cross-machine sync.
- Deduplicate: use session ID + timestamp to avoid processing the same event twice from both channels.

### 5. Plan-Aware Classification Routing `[CHANGED]`

Update routing in the background service worker:

| Plan | Route |
|---|---|
| Free (BYOK) | Extension → LLM provider directly (existing) |
| Cloud BYOK | Extension → LLM provider directly (existing) + submit to cloud |
| Cloud Managed | Extension → `POST /classify` (existing) |

**Implementation:**
- The background service worker already has routing logic between BYOK and Foqus account mode.
- Extend this to be plan-aware: Free BYOK vs Cloud BYOK (both call provider directly, but Cloud also submits to backend).
- For Cloud BYOK, after local classification, submit result to cloud as per §3.

### 6. Auth Flow — Plan/Entitlement Fetch `[CHANGED]`

After successful login:

1. Call `/auth/me` or `/subscriptions/status` to get plan type.
2. Store plan type in `chrome.storage.local`.
3. Gate features:
   - Free: no device registration, no cloud sync, no full analytics link.
   - Cloud: enable device registration, cloud submission, SignalR, full analytics link.
4. Handle plan changes: poll `/subscriptions/status` periodically or use SignalR `PlanChanged`.

**Token refresh:**
- Supabase JS client handles refresh automatically.
- Verify it aligns with Epic 1 §10 (refresh when < 5 min remaining, retry 3 times, then prompt re-login).

### 7. "View Full Analytics" Link `[NEW]`

Add links to the web app in:

- **Popup:** "View full analytics →" link below the basic analytics summary.
- **Options page:** "Full Analytics" section with "Open in web app →" link.

**Behavior:**
- Opens `https://app.foqus.me/analytics` in a new tab.
- Only shown when signed in with a cloud plan.
- For free users: "Upgrade for full analytics →" with subtle styling.

### 8. Basic Analytics — Spec Alignment `[CHANGED]`

Audit existing analytics against Epic 1 §2:

**Already covered:**
- Session summaries (focus %, aligned/distracted time, distraction count, top domains) ✓
- Today/7d/30d aggregations ✓

**Potentially missing or needing adjustment:**
- Session list view (currently shows summaries, may need a list format matching the spec).
- "Simple counts" (total sessions, total focused time, total distracted time) as a dedicated aggregate.
- Canonical field names matching Epic 1 definitions.

### 9. Multi-Provider BYOK `[OPEN QUESTION]`

The extension currently supports **only OpenAI** for BYOK. The Windows app supports OpenAI, Anthropic, and Google via LlmTornado.

**Options:**

| Option | Effort | Notes |
|---|---|---|
| Keep OpenAI only | Low | Simplest. Users who want other providers use the Windows app or Cloud Managed. |
| Add Anthropic + Google | Medium | Requires implementing provider abstraction in the extension. TypeScript equivalents of LlmTornado calls. |
| Use backend passthrough for multi-provider | Low-Medium | Cloud BYOK users send `X-Api-Key` to `POST /classify` which routes to any provider. But this adds backend dependency for BYOK. |

**Recommendation:** Keep OpenAI only for the MVP. Add multi-provider in a follow-up if demand warrants it. The extension is a secondary classification source; desktop app handles the multi-provider case.

**Decision required before implementation.**

### 10. Alignment Indicator Refinement `[CHANGED]`

Review existing indicators for plan-aware states:

- **Popup:** Current session status, alignment badge. Add plan badge and upgrade CTA for free users.
- **Badge/icon:** `chrome.action.setBadgeText` already used. Ensure it reflects classification state cleanly.
- **Distraction overlay:** Already exists. Add plan-aware messaging if relevant (e.g., "Classification paused — trial expired").
- **Tooltip on navigation:** Already fires on page change. Review for noise level — ensure configurable.

---

## Technical Notes

- The extension uses Manifest V3 with a background service worker that can be terminated.
- Use `chrome.alarms` for periodic tasks (heartbeat, SignalR reconnection) to survive service worker termination.
- `chrome.storage.local` has a 10 MB quota (UNLIMITED with `unlimitedStorage` permission if needed).
- All async operations in the service worker should be careful about the service worker lifecycle.
- Follow existing TypeScript conventions in `browser-extension/`.
- Tests: update/add Vitest tests for new functionality.

---

## Exit Criteria

- [ ] A user can use BYOK in the extension without signing in
- [ ] A user can sign in via magic link from the extension
- [ ] After login, the user can choose a plan (Free / Cloud BYOK / Cloud Managed)
- [ ] Free users see local basic analytics and an upgrade CTA
- [ ] Cloud users' sessions are submitted to the backend with device attribution
- [ ] Cloud Managed users' classifications route through the backend
- [ ] Cloud BYOK users classify locally and submit results to the cloud
- [ ] The extension registers as a device and sends heartbeats when signed in
- [ ] The extension connects to SignalR for cross-device sync when on a cloud plan
- [ ] The local WebSocket still works for same-machine desktop app communication
- [ ] "View full analytics" opens the web app
- [ ] The extension appears in the integrations page with online/offline status
- [ ] Multi-provider BYOK decision is documented (even if deferred)
