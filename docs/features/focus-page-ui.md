# Focus Page UI

## Overview

The main screen displays a **single-session focus interface**. When no session is active, a **Start Session Form** is shown. When a session is in progress, an **Active Session Card** is displayed with real-time focus monitoring. A **Focus Status Control** (`FocusStatusControl`) appears above the main content showing the current foreground window and real-time focus classification. The status control is a standalone `UserControl` with its own `FocusStatusViewModel`. The layout uses a fixed-height status area to avoid UI jumping when switching apps.

## Layout Structure

```
┌─────────────────────────────────────────────────────────────────┐
│  Window info (compact)                                           │
│  Process: msedge | Window: Booking.com...  [full text in tooltip]│
│  Visit: 00:02:15 · Total: 00:05:42                               │
├─────────────────────────────────────────────────────────────────┤
│  Focus Status Control (fixed min height ~72px)                   │
│  [Icon] FOCUSED / UNCLEAR / DISTRACTED                           │
│        Reason from AI (one line, ellipsis; full text in tooltip) │
│  OR: [ProgressRing] Evaluating focus...                          │
├─────────────────────────────────────────────────────────────────┤
│  Settings · Help                                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Start Session Form (when no active session)                     │
│  OR                                                               │
│  Active Session Card (when session in progress)                  │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

## Start Session Form

Shown when no session is active (`ShowStartForm = true`).

### Components

- **Title:** "Start a Focus Session"
- **Session Title Input:** Required field, max 200 characters
  - Placeholder: "What are you working on?"
- **Context Input:** Optional field, max 200 characters, multiline
  - Placeholder: "Context hints for AI (optional)"
- **Start Session Button:** Primary action, enabled when title is not empty

### ViewModel Properties (`FocusPageViewModel`)

- `ShowStartForm` (bool) – true when `ActiveSession == null`
- `StartSessionTitle` (string) – bound to title input
- `StartSessionContext` (string) – bound to context input
- `StartSessionCommand` (IRelayCommand) – executes when Start Session is clicked

## Active Session Card

Shown when a session is in progress (`IsActiveSessionVisible = true`).

### Components

- **Session Title:** Large, bold text (22px)
- **Session Context:** Secondary text, shown if provided
- **Metrics Grid:**
  - **Elapsed Time:** Icon + label + formatted time (HH:MM:SS)
  - **Focus Score:** Icon + label + percentage (visible when user is signed in)
- **Action Buttons:**
  - **Pause/Resume:** Toggle button based on `IsSessionPaused`
  - **End Session:** Primary action, completes the session

### ViewModel Properties (`FocusPageViewModel`)

- `IsActiveSessionVisible` (bool) – true when `ActiveSession != null`
- `ActiveSession` (UserSession?) – the current session being tracked
- `SessionElapsedTime` (string) – formatted elapsed time (HH:MM:SS)
- `CurrentFocusScorePercent` (int) – focus score percentage
- `IsFocusScorePercentVisible` (bool) – `Status.IsMonitoring && AccountSection.IsAuthenticated`
- `IsSessionPaused` (bool) – whether session is currently paused
- `PauseSessionCommand` (IRelayCommand) – pauses the session
- `ResumeSessionCommand` (IRelayCommand) – resumes the session
- `EndSessionCommand` (IRelayCommand) – completes the session

## Focus Status Control

A standalone `UserControl` (`FocusStatusControl` in `Views/Controls/`) with its own `FocusStatusViewModel`. Visible only when a session is **In Progress** and monitoring is active.

### States

| State | When | Visual |
|-------|------|--------|
| **Evaluating** | Window just changed; waiting for AI | ProgressRing + "Evaluating focus..." |
| **Focused** | Score 6–10 | Fire icon, large green "FOCUSED" text, reason below |
| **Unclear** | Score 4–5 | Question icon, large purple "UNCLEAR" text, reason below |
| **Distracted** | Score 1–3 | Warning icon, large orange "DISTRACTED" text, reason below |

### Design

- **Fixed min height** (72px) so the control does not collapse while classifying; prevents layout jump.
- **Large status text** (20px, bold) with color by state (green / purple / orange).
- **Custom icons** per state: `icon-focused.svg`, `icon-unclear.svg`, `icon-distracted.svg` in `Assets/`.
- **Reason text** from AI shown in smaller secondary style, max 2 lines with ellipsis.

### ViewModel Properties (`FocusStatusViewModel`)

The `FocusStatusViewModel` subscribes directly to `IFocusSessionOrchestrator.StateChanged` and owns all classification display state:

- `IsMonitoring` – entire control visible when true. Set by parent `FocusPageViewModel`.
- `CurrentProcessName`, `CurrentWindowTitle` – foreground window info.
- `IsClassifying` – shows ProgressRing and "Evaluating focus...".
- `HasCurrentFocusResult` – whether a classification result has been received.
- `ShowCheckingMessage` – `IsMonitoring && !HasCurrentFocusResult`.
- `FocusScoreCategory` – "Focused" | "Unclear" | "Distracted".
- `FocusStatusIcon` – ms-appx URI to the SVG for current state.
- `FocusAccentBrushKey` – theme resource key for the current state accent color.
- `FocusScore`, `FocusReason` – raw score and AI reason.
- `ShowMarkOverrideButton`, `MarkOverrideButtonText` – manual override controls.
- `MarkFocusOverrideCommand` – toggles current classification between focused/distracting.
- `Reset()` – called by parent when a session ends to clear all display state.

## Session Flow

1. **Start:** User enters session title (and optional context) → clicks "Start Session"
   - `StartSessionCommand` creates session as InProgress
   - `ActiveSession` is set, `ShowStartForm` becomes false
   - Active Session Card is displayed, `FocusStatusControl` becomes visible
   
2. **During Work:** User switches windows, focus is monitored
   - `FocusStatusControl` updates with real-time classification via `FocusStatusViewModel`
   - Elapsed time increments every second
   
3. **Pause/Resume:** User can pause and resume the session
   - `PauseSessionCommand` stops monitoring and time tracking
   - `ResumeSessionCommand` resumes monitoring and time tracking
   
4. **End:** User clicks "End Session"
   - `EndSessionCommand` finalizes focus score, ends orchestrator session
   - `ActiveSession` is cleared, `ShowStartForm` becomes true
   - `FocusStatusViewModel.Reset()` clears classification state
   - Start Session Form is displayed

## Styles and Resources

- **FbAccentButtonStyle** – Primary button style for Start Session and End Session
- **FbOutlineButtonStyle** – Secondary button style for Pause/Resume
- **FbCardBackgroundBrush** – Background for Start Form and Active Session Card
- **FbCardBorderBrush** – Border for cards
- **FbStatusBadgeInProgressStyle** – Style for "In Progress" badge
- **FbFocusStatusBarStyle** – Border for the focus bar (card background, border, padding, MinHeight 72)
- **FbFocusStatusTextStyle** – 20px bold for "FOCUSED" / "UNCLEAR" / "DISTRACTED"
- **FbWindowInfoTextStyle** – 11px secondary for process/window and time lines
- **FocusScoreToBrushConverter** – Maps `FocusScore` (int) to the correct accent brush for border/dot
- **FocusScoreToTextColorConverter** – Maps `FocusScore` to the correct text color for the status label
- **FocusScorePercentFormatConverter** – Formats focus score percent (e.g., "85%")

## Related

- [Focus Score](focus-score.md) – How alignment scores and focus % are computed and persisted.
- [App-Extension Integration](../app-extension-integration.md) – How sessions are synced between app and browser extension.
