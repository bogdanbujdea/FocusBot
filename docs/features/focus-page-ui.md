# Focus Page UI

## Overview

The main screen displays a **single-task focus interface**. When no task is active, a **Start Task Form** is shown. When a task is in progress, an **Active Task Card** is displayed with real-time focus monitoring. A **Focus Status Bar** appears above the main content showing the current foreground window and real-time focus classification. The layout uses a fixed-height status area to avoid UI jumping when switching apps.

## Layout Structure

```
┌─────────────────────────────────────────────────────────────────┐
│  Window info (compact)                                           │
│  Process: msedge | Window: Booking.com...  [full text in tooltip]│
│  Visit: 00:02:15 · Total: 00:05:42                               │
├─────────────────────────────────────────────────────────────────┤
│  Focus Status Bar (fixed min height ~72px)                       │
│  [Icon] FOCUSED / UNCLEAR / DISTRACTED                           │
│        Reason from AI (one line, ellipsis; full text in tooltip) │
│  OR: [ProgressRing] Evaluating focus...                          │
├─────────────────────────────────────────────────────────────────┤
│  Settings · Help                                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Start Task Form (when no active task)                           │
│  OR                                                               │
│  Active Task Card (when task in progress)                        │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

## Start Task Form

Shown when no task is active (`ShowStartForm = true`).

### Components

- **Title:** "Start a Focus Session"
- **Task Title Input:** Required field, max 200 characters
  - Placeholder: "What are you working on?"
- **Context Input:** Optional field, max 200 characters, multiline
  - Placeholder: "Context hints for AI (optional)"
- **Start Task Button:** Primary action, enabled when title is not empty
- **View History Link:** Secondary action, navigates to history page

### ViewModel Properties

- `ShowStartForm` (bool) – true when `ActiveTask == null && !_extensionHasActiveTask`
- `StartTaskTitle` (string) – bound to title input
- `StartTaskContext` (string) – bound to context input
- `StartTaskCommand` (IRelayCommand) – executes when Start Task is clicked

## Active Task Card

Shown when a task is in progress (`IsActiveTaskVisible = true`).

### Components

- **Status Badge:** "In Progress" with icon
- **Task Title:** Large, bold text (20px)
- **Task Context:** Secondary text, shown if provided
- **Metrics Grid:**
  - **Elapsed Time:** Icon + label + formatted time (HH:MM:SS)
  - **Focus Score:** Icon + label + percentage (if available)
  - **Distractions:** Icon + label + count
- **Action Buttons:**
  - **Pause/Resume:** Toggle button based on `IsTaskPaused`
  - **End Task:** Primary action, completes the task

### ViewModel Properties

- `IsActiveTaskVisible` (bool) – true when `ActiveTask != null || _extensionHasActiveTask`
- `ActiveTask` (UserTask?) – the current task being worked on
- `TaskElapsedTime` (string) – formatted elapsed time (HH:MM:SS)
- `CurrentFocusScorePercent` (int?) – focus score percentage
- `IsFocusScorePercentVisible` (bool) – whether focus score is available
- `LiveDistractionCount` (int) – number of distractions during this session
- `IsTaskPaused` (bool) – whether task is currently paused
- `PauseTaskCommand` (IRelayCommand) – pauses the task
- `ResumeTaskCommand` (IRelayCommand) – resumes the task
- `EndTaskCommand` (IRelayCommand) – completes the task

## Focus Status Bar

Visible only when a task is **In Progress** and monitoring is active.

### States

| State | When | Visual |
|-------|------|--------|
| **Evaluating** | Window just changed; waiting for OpenAI | ProgressRing + "Evaluating focus..." |
| **Focused** | Score 6–10 | Fire icon, large green "FOCUSED" text, reason below |
| **Unclear** | Score 4–5 | Question icon, large purple "UNCLEAR" text, reason below |
| **Distracted** | Score 1–3 | Warning icon, large orange "DISTRACTED" text, reason below |

### Design

- **Fixed min height** (72px) so the bar does not collapse while classifying; prevents layout jump.
- **Large status text** (20px, bold) with color by state (green / purple / orange).
- **Custom icons** per state: `icon-focused.svg`, `icon-unclear.svg`, `icon-distracted.svg` in `Assets/`.
- **Reason text** from AI shown in smaller secondary style, max 2 lines with ellipsis.

### ViewModel Properties

- `IsMonitoring` – entire bar (window info + focus bar) visible when true.
- `IsFocusScoreVisible` – focus bar visible when classifying or when we have a result.
- `IsClassifying` – shows ProgressRing and "Evaluating focus...".
- `IsFocusResultVisible` – shows icon + category + reason (inverse of classifying).
- `FocusScoreCategory` – "Focused" | "Unclear" | "Distracted".
- `FocusStatusIcon` – ms-appx URI to the SVG for current state.
- `FocusScore`, `FocusReason` – raw score and AI reason.

## Task Flow

1. **Start:** User enters task title (and optional context) → clicks "Start Task"
   - `StartTaskCommand` creates task as InProgress
   - Loads board, which starts monitoring
   - `ActiveTask` is set, `ShowStartForm` becomes false
   - Active Task Card is displayed
   
2. **During Work:** User switches windows, focus is monitored
   - Focus status bar updates with real-time classification
   - Elapsed time increments every second
   - Distraction count increases on misaligned windows
   
3. **Pause/Resume:** User can pause and resume task
   - `PauseTaskCommand` stops monitoring and time tracking
   - `ResumeTaskCommand` resumes monitoring and time tracking
   
4. **End:** User clicks "End Task"
   - `EndTaskCommand` finalizes focus score, marks task as Done
   - `ActiveTask` is cleared, `ShowStartForm` becomes true
   - Start Task Form is displayed

## Styles and Resources

- **FbAccentButtonStyle** – Primary button style for Start Task and End Task
- **FbOutlineButtonStyle** – Secondary button style for Pause/Resume
- **FbCardBackgroundBrush** – Background for Start Form and Active Task Card
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
- [App-Extension Integration](../app-extension-integration.md) – How tasks are synced between app and browser extension.
