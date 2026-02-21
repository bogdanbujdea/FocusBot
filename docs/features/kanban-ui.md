# Kanban Board UI

## Overview

The main screen is a **Kanban board** with three columns (TO DO, IN PROGRESS, DONE). When a task is in progress, a **Focus Status Bar** appears above the board showing the current foreground window and real-time focus classification. The layout uses a fixed-height status area to avoid UI jumping when switching apps.

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
│  + Add Task                    Settings                          │
├──────────────┬─────────────────────┬────────────────────────────┤
│  TO DO       │  IN PROGRESS         │  DONE                      │
│  (count)     │  (count)             │  (count)                   │
│  ─────────   │  ─────────           │  ─────────                 │
│  [Cards]     │  [Active card]       │  [Cards]                   │
└──────────────┴─────────────────────┴────────────────────────────┘
```

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

## Task Cards

### TO DO

- Left accent: neutral (purple).
- Content: title, total elapsed time, Start / View / Edit / Delete.

### IN PROGRESS

- **Left accent and status dot** use **dynamic color** from current focus classification:
  - Focused (6–10): green (`FbAlignedAccentBrush`).
  - Unclear (4–5): purple (`FbNeutralAccentBrush`).
  - Distracted (1–3): orange (`FbMisalignedAccentBrush`).
- Content: "Active" badge, task timer, Focus: X%, title, Done / Stop / View / Edit / Delete.
- Border color updates when the user switches windows and classification returns.

### DONE

- Left accent: muted green (same brush, 0.5 opacity).
- Content: "Completed" badge, total time, Focus: X% (if saved), title, View / Edit / Delete.

## Styles and Resources

- **FbFocusStatusBarStyle** – Border for the focus bar (card background, border, padding, MinHeight 72).
- **FbFocusStatusTextStyle** – 20px bold for "FOCUSED" / "UNCLEAR" / "DISTRACTED".
- **FbWindowInfoTextStyle** – 11px secondary for process/window and time lines.
- **FocusScoreToBrushConverter** – Maps `FocusScore` (int) to the correct accent brush for border/dot.
- **FocusScoreToTextColorConverter** – Maps `FocusScore` to the correct text color for the status label.

## Related

- [Focus Score](focus-score.md) – How alignment scores and focus % are computed and persisted.
- [Add Task Popup](add-task-popup.md) – Add task flow.
- [Edit Task Popup](edit-task-popup.md) – Edit task flow.
