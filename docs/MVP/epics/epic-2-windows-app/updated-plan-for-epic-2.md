Plan: Epic 2 — Windows App Backend Integration (Revised)
TL;DR: Turn the desktop app into a thin client by routing all classification through the backend (POST /classify), deleting ~2,000 lines of local LLM/scoring/analytics/subscription infrastructure, and replacing it with ~250–350 lines of backend-backed services. Add device registration and heartbeat. No SignalR — the desktop only talks to the browser extension on the same machine via the existing local WebSocket. Before deleting the desktop's LLM code, bring the WebAPI's classification service to full parity (prompt alignment, structured errors, key validation endpoint). At the end, desktop app and backend run together locally with classification, sessions, device registration, and plan management all working end-to-end.

Key Decisions (Recap + Updates)
No SignalR. Desktop communicates with extension via local WebSocket only. Cross-machine sync deferred to a later epic.
All classification routes through backend — even free BYOK (backend forwards with user's key via X-Api-Key header)
POST /classify is stateless (uses cache, no event records); enriched session summary computed locally at session end
Client-side SQLite cache stays (avoid redundant backend calls)
DeviceType enum: Desktop, Extension only (no Web)
Focus score computed locally by a lightweight tracker (~60 lines replaces 294-line FocusScoreService)
Plan changes detected by polling GET /subscriptions/status (no SignalR push)
One active focus task per user; if another device has an active session, prompt user to stop/pause it
Phase 0 — Backend Prep (Prerequisites)
Step 0.1 — Bring WebAPI classification to parity with desktop

Before deleting the desktop's LlmService, the WebAPI classification must be at least as capable. Gaps found:

Prompt alignment: Update the system prompt in ClassificationService.cs (lines 25–44) to match the desktop's 5-tier scoring guide (1-2, 3-4, 5-6, 7-8, 9-10) from LlmService.cs (lines 23–42). The WebAPI currently has only 3 anchor points (1, 5, 10), which produces less granular scores.
Structured error responses: The WebAPI currently returns raw InvalidOperationException text. Add structured error handling: map provider 429 (rate limit) → HTTP 429, invalid key → HTTP 401 with a clear error body, provider unavailable → HTTP 502. This mirrors the desktop's error mapping at LlmService.cs:222-245.
Key validation endpoint: Add POST /classify/validate-key that takes providerId, modelId, apiKey and makes a lightweight test request (like the desktop's ValidateCredentialsAsync at LlmService.cs:187-218). Returns valid/invalid/provider-unavailable status.
Step 0.2 — Device entity + CRUD endpoints

Add Device entity in Entities with fields: Id (Guid), UserId (FK), DeviceType enum (Desktop, Extension), Name, Fingerprint, AppVersion, Platform, LastSeenAtUtc, CreatedAtUtc
Add nullable DeviceId FK on Session.cs
Create Features/Devices/ slice: DevicesEndpoints.cs, DeviceService.cs, DeviceDtos.cs, SLICE.md
Endpoints: POST /devices (register), GET /devices (list user's devices), PUT /devices/{id}/heartbeat, DELETE /devices/{id}
Heartbeat updates LastSeenAtUtc and AppVersion/Platform
Online threshold: LastSeenAtUtc within 3 minutes
Step 0.3 — PlanType on Subscription

Add PlanType enum (FreeBYOK, CloudBYOK, CloudManaged) to Subscription.cs
Update GET /subscriptions/status in SubscriptionEndpoints.cs to return PlanType
Update GET /auth/me in AuthEndpoints.cs to include plan info
Step 0.4 — Session DeviceId attribution

Update POST /sessions in SessionEndpoints.cs to accept deviceId
Update POST /sessions/{id}/end to accept enriched summary: topDistractingApps, topAlignedApps, contextSwitchCount, deviceId
Enforce one active session per user (current behavior); when a second device tries to start, return a 409 with the existing session info so the client can prompt the user
Step 0.5 — EF Core migration

Add migration for: Devices table, Session.DeviceId FK, Subscription.PlanType
Phase 1 — Desktop App: Mass Deletion (~2,000 lines)
Delete code that the backend now handles. Do this before adding new code to keep the diff clean.

Step 1.1 — Delete LLM infrastructure

Delete LlmService.cs (270 lines)
Delete AlignmentClassificationCacheDecorator.cs (90 lines)
Delete EmbeddedManagedKeyProvider.cs (241 lines)
Delete ILlmService interface from Core/Interfaces/
Delete IManagedKeyProvider interface from Core/Interfaces/
Delete ClassifyAlignmentResponse from Core/Entities/
Remove LlmTornado NuGet package from FocusBot.Infrastructure.csproj
Step 1.2 — Delete Windows Store subscription

Delete SubscriptionService.cs (160 lines)
Delete MockSubscriptionService.cs (65 lines)
Delete StoreContextHolder.cs (14 lines)
Delete SubscriptionInfo entity and PurchaseResult enum from Core/Entities/
Redefine ISubscriptionService as a thin backend-backed plan check (or merge into new IPlanService)
Step 1.3 — Delete local trial

Delete TrialService.cs (70 lines)
Delete ITrialService from Core/Interfaces/ (backend manages trial via POST /subscriptions/trial)
Step 1.4 — Delete heavy scoring/analytics services

Delete FocusScoreService.cs (294 lines)
Delete DistractionDetectorService.cs (105 lines)
Delete TaskSummaryService.cs (70 lines)
Delete SessionDistractionAnalyticsService.cs (60 lines)
Delete DailyAnalyticsService.cs (175 lines)
Delete corresponding interfaces: IFocusScoreService, IDistractionDetectorService, ITaskSummaryService, IDistractionAnalyticsService, IDailyAnalyticsService
Delete FocusSegment, DistractionEvent, DailyFocusAnalytics entities from Core/Entities/
Delete IDistractionEventRepository and its implementation
Step 1.5 — Slim down SQLite

Remove FocusSegments, DistractionEvents, DailyFocusAnalytics, WindowContexts DbSets from AppDbContext.cs — keep UserTasks and AlignmentCacheEntries
Add a migration that drops the deleted tables
Step 1.6 — Update DI registration

Remove all deleted services from DI wiring in App.xaml.cs (or wherever registered)
This causes compile errors in ViewModels — addressed in Phase 2
Phase 2 — Desktop App: New Backend-Backed Services (~250–350 lines)
Step 2.1 — IClassificationService (replaces ILlmService)

New interface in Core: ClassifyAsync(processName, windowTitle, taskText, taskHints, CancellationToken) → Result<AlignmentResult>
New implementation in Infrastructure (~40 lines): check SQLite cache → if miss, call FocusBotApiClient.ClassifyAsync() → cache result → return
For BYOK users, include the API key in the request (read from settings, pass to API client which sets X-Api-Key header)
For BYOK users, also pass ProviderId and ModelId (currently missing from ClassifyPayload and FocusBotApiClient)
Step 2.2 — ILocalSessionTracker (replaces FocusScoreService + DistractionDetector + TaskSummary)

New interface in Core (~15 lines): Start(), RecordClassification(processName, AlignmentResult), HandleIdle(bool isIdle), GetFocusScore() → int, GetSessionSummary() → SessionSummary, Reset()
New implementation in Infrastructure (~60–80 lines):
In-memory Dictionary<string, (int alignedSeconds, int distractedSeconds)> per process
Running totals: _focusedSeconds, _distractedSeconds, _distractionCount
Distraction detection: track when state transitions aligned → not-aligned (increment count)
GetSessionSummary() returns: focused time, distracted time, score, top distracting apps (sorted by distracted seconds), top aligned apps, distraction count
Handles idle by pausing time accumulation
Step 2.3 — IDeviceService (new)

New interface in Core (~10 lines): RegisterAsync(), SendHeartbeatAsync(), DeregisterAsync(), GetDeviceId() → string?
New implementation in Infrastructure (~50 lines):
Generate stable GUID fingerprint → persist in settings
Call POST /devices via FocusBotApiClient → store returned deviceId
Heartbeat: PUT /devices/{id}/heartbeat every 60s (timer-based)
Handle 401 (refresh token) and 404 (re-register)
Step 2.4 — IPlanService (replaces ISubscriptionService + ITrialService)

New interface in Core (~10 lines): GetCurrentPlanAsync() → PlanType, RefreshPlanAsync(), PlanChanged event
New implementation in Infrastructure (~40 lines):
Fetch from GET /subscriptions/status via FocusBotApiClient
Cache locally in settings (survives restarts)
Poll every 5 minutes while app is running (no SignalR)
Expose PlanType enum: FreeBYOK, CloudBYOK, CloudManaged
Step 2.5 — Expand FocusBotApiClient

Add to existing FocusBotApiClient.cs:
RegisterDeviceAsync(request) → POST /devices
SendHeartbeatAsync(deviceId) → PUT /devices/{id}/heartbeat
DeregisterDeviceAsync(deviceId) → DELETE /devices/{id}
GetPlanStatusAsync() → GET /subscriptions/status
StartTrialAsync() → POST /subscriptions/trial
ValidateKeyAsync(providerId, modelId, apiKey) → POST /classify/validate-key
Update ClassifyAsync to accept and pass X-Api-Key header, ProviderId, and ModelId for BYOK
Update ClassifyPayload in ApiModels.cs to include ProviderId, ModelId
Update EndSessionAsync to include enriched summary (top apps, distraction count)
Phase 3 — Desktop App: ViewModel Refactoring
Step 3.1 — Rewrite FocusPageViewModel classification loop

Current: FocusPageViewModel.cs at ~1,620 lines
Replace classification orchestration (~lines 780–830) with a call to the new IClassificationService
Replace the every-second tick handler (~lines 540–600): remove FocusScoreService/DistractionDetector/DailyAnalytics calls, replace with ILocalSessionTracker.RecordClassification() and GetFocusScore()
Remove all segment persistence logic
Remove daily analytics refresh logic
Expected reduction: ~400–500 lines, bringing the VM closer to the 200-line guideline
Consider extracting remaining concerns (overlay management, extension integration) into separate services
Step 3.2 — Session lifecycle with backend

On task start (if cloud plan): call POST /sessions with deviceId → store server sessionId
Handle 409 (another device has active session) → show dialog: "You have an active session on [device name]. Stop it and start here?"
During session: ILocalSessionTracker tracks everything locally
On task end (if cloud plan): call POST /sessions/{id}/end with ILocalSessionTracker.GetSessionSummary()
On task end (free plan): store summary locally only
Handle offline: queue session start/end calls, retry on reconnect
Step 3.3 — Refactor ApiKeySettingsViewModel

Current: ApiKeySettingsViewModel.cs at 465 lines
Delete Windows Store subscription status section
Delete trial countdown section
Keep BYOK provider/key management (OpenAI, Anthropic, Google selection)
Update key validation to call new POST /classify/validate-key endpoint via FocusBotApiClient
Add plan display: show current plan from IPlanService
Add "Upgrade"/"Change plan" button → opens Paddle checkout in browser
Step 3.4 — Refactor HistoryViewModel

Current: HistoryViewModel.cs at 297 lines
For cloud users: fetch sessions from GET /sessions via FocusBotApiClient
For free users: keep local SQLite query
Add "Open full analytics" button → opens app.foqus.me/analytics in browser (cloud users only)
For free users: show upgrade CTA
Phase 4 — Plan Selection UI
Step 4.1 — New PlanSelectionPage

New XAML page in Views with corresponding ViewModel
Three plan cards: Free BYOK, Cloud BYOK, Cloud Managed
Feature comparison (classification, analytics, sync, price)
Current plan indicator
Free → instant. Cloud plans → open Paddle checkout URL in default browser
After checkout, poll GET /subscriptions/status until plan updates
Step 4.2 — Post-login flow

After successful Supabase magic link auth:
Call GET /auth/me (auto-provisions user)
Call GET /subscriptions/status to get plan
If no plan → navigate to PlanSelectionPage
If plan exists → register device (POST /devices), start heartbeat
Step 4.3 — Settings integration

Add "Plan & Billing" section to settings
Show current plan, "Change plan" link
"Manage billing" → opens web app billing page in browser
Phase 5 — Device Registration & Heartbeat
Step 5.1 — Auto-registration on cloud login

After login + plan ≥ Cloud:
Generate fingerprint GUID (first time only, persist in settings)
Call POST /devices with type Desktop, machine hostname, fingerprint, app version, platform
Store deviceId in settings
Step 5.2 — Heartbeat timer

Start a 60-second PeriodicTimer when signed in with cloud plan
Call PUT /devices/{id}/heartbeat
Handle 404 → re-register device
Handle 401 → refresh token, retry
Stop timer on logout or plan downgrade
Step 5.3 — Deregistration on logout

On explicit sign-out, call DELETE /devices/{id}
Clear local deviceId from settings
Phase 6 — Overlay & UX Polish
Step 6.1 — Overlay state refinement

Ensure the floating overlay handles: Neutral (no task), Loading (classifying), Aligned/NotAligned/Unclear, Error (network/provider), Idle (tracking paused)
Add tooltip with brief status explanation
Plan-aware messaging: if managed trial expired, show "Trial expired — upgrade to continue"
Step 6.2 — BYOK key validation

On API key save, call POST /classify/validate-key via FocusBotApiClient
Show status: valid / invalid / provider unavailable
Handle key removal gracefully
Phase 7 — Auth & Token Alignment
Step 7.1 — Token refresh

Update RefreshTokenAsync() in SupabaseAuthService.cs to trigger when token has < 5 minutes remaining
On refresh failure: retry 3 times, then prompt re-login
Ensure FocusBotApiClient reads fresh token on each request
Step 7.2 — Plan-gated features

IPlanService.GetCurrentPlanAsync() checks before: device registration (cloud only), heartbeat (cloud only), cloud session submission (cloud only), "Open full analytics" CTA (cloud only, upgrade CTA for free)
Phase 8 — Tests
Step 8.1 — Delete tests for deleted services

Remove tests for LlmService, FocusScoreService, DistractionDetectorService, TrialService, SubscriptionService (Store), TaskSummaryService, DailyAnalyticsService, SessionDistractionAnalyticsService
Step 8.2 — New desktop unit tests

ClassificationServiceShould — cache hit returns cached, cache miss calls API then caches, BYOK sends provider/key
LocalSessionTrackerShould — records classifications, computes score, tracks per-app times, detects distraction transitions, handles idle, returns correct summary
DeviceServiceShould — registers device, sends heartbeat, deregisters, handles 404/401
PlanServiceShould — fetches plan, caches, polls periodically
Step 8.3 — New WebAPI tests

Device CRUD endpoint tests (registration, heartbeat, deletion, list)
Session with DeviceId attribution, 409 on duplicate active session
Subscription with PlanType
Classification prompt parity (verify 5-tier scoring)
Classification with X-Api-Key BYOK passthrough (verify provider/model forwarding)
POST /classify/validate-key endpoint tests
Structured error responses (429 → 429, invalid key → 401, etc.)
Step 8.4 — Integration tests

Extend CustomWebApplicationFactory for Device scenarios
End-to-end: register device → start session → classify → end session with enriched summary → verify data
Phase 9 — Update Epic Documentation
Record the decisions made during this planning session so Epics 4, 5, and 6 build on the same assumptions.

Step 9.1 — Update Epic 4 (Browser Extension)

All classification routes through backend POST /classify (extension already does this partially — make it the only path)
Extension sends BYOK key via X-Api-Key header + ProviderId/ModelId
DeviceType for extension is Extension (no Web type)
Same-machine sync with desktop via local WebSocket remains
No SignalR — extension on a different machine operates independently (sync deferred)
Session conflict: if desktop has an active session, POST /sessions returns 409 → extension prompts user
Local session tracking with enriched summary at session end (same ILocalSessionTracker pattern)
Plan polling via GET /subscriptions/status (no SignalR push)
Step 9.2 — Update Epic 5 (Full Analytics)

Analytics data comes from enriched session summaries (not per-classification events)
Available per-session data: focused time, distracted time, focus score, distraction count, top distracting apps, top aligned apps, context switch count, device info
No classification-event timeline (decided against storing per-event records server-side)
Web app queries GET /sessions with filters — all analytics computed from session summaries
Implications: can show session-level charts (score over time, focused vs distracted per day, top apps across sessions) but NOT per-minute heatmaps or real-time classification timelines
Step 9.3 — Update Epic 6 (Cross-Device Sync)

No SignalR in MVP — cross-machine real-time sync is deferred
Sync model: each device submits sessions independently to backend; web app shows unified view
Same-machine desktop ↔ extension sync via local WebSocket (existing)
Session conflict resolution: backend enforces one active session per user; second device gets 409 → prompt to stop/pause
Plan changes detected by polling (5-min interval), not push
Note: SignalR can be added in a future iteration when real-time cross-device push becomes a priority
Step 9.4 — Update Epic 2 doc itself

Remove all SignalR references from tasks 4 ("SignalR Client for Server-Mediated Sync") and related mentions
Update task 5 (classification routing) to reflect all-through-backend approach
Mark decisions inline: stateless classify, enriched session summaries, no SignalR, local WS stays
Verification
Local end-to-end test:

Start PostgreSQL: docker run -d --name focusbot-pg -e POSTGRES_DB=focusbot -e POSTGRES_USER=focusbot -e POSTGRES_PASSWORD=focusbot_dev -p 5432:5432 postgres:16-alpine
Start WebAPI: dotnet run --project [FocusBot.WebAPI.csproj](http://_vscodecontentref_/32) --launch-profile http
Launch desktop app → sign in → verify plan fetch from GET /subscriptions/status
Select a plan → verify device registration at POST /devices → verify heartbeat at PUT /devices/{id}/heartbeat
Start a task → verify POST /sessions with deviceId → verify classification calls POST /classify
Switch windows → verify overlay updates → verify SQLite cache prevents redundant API calls
End task → verify POST /sessions/{id}/end with enriched summary (top apps, distraction count)
Check GET /sessions → verify per-app analytics present in summary
Run all tests: dotnet test [FocusBot.Core.Tests](http://_vscodecontentref_/33)/, dotnet test [FocusBot.WebAPI.Tests](http://_vscodecontentref_/34)/, dotnet test [FocusBot.WebAPI.IntegrationTests](http://_vscodecontentref_/35)/
Deletion verification:

LlmTornado not in any desktop .csproj
AppDbContext has only UserTasks + AlignmentCacheEntries DbSets
FocusPageViewModel is under 400 lines (target ~200–300)
Build passes with TreatWarningsAsErrors
Decisions Log
Decision	Chose	Over
Classification routing	All through backend (even free BYOK)	Keep local LLM service for free users
SignalR	Not in MVP	SignalR for cross-device sync
Same-machine sync	Local WebSocket (existing)	Drop local WS for SignalR
Event storage	No per-classification events server-side	Store event records per classify call
Session analytics	Enriched summary at session end (top apps, score, counts)	Summary-only or raw events
Focus score	Lightweight local tracker (~60 lines)	Heavy FocusScoreService or pure backend
Client cache	SQLite (existing)	In-memory dictionary
DeviceType enum	Desktop, Extension	Including Web
Plan change detection	Poll every 5 min	SignalR push
Session conflict	409 from backend + client prompt	Silent override or auto-pause