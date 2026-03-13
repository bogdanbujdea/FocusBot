# Subfeature 2 – Daily Analytics Header Implementation

## Overview

This document captures how the **top-of-board daily analytics header** is implemented on `KanbanBoardPage` to surface today's focus behavior as the primary dashboard element.

- Location: `src/FocusBot.App/Views/KanbanBoardPage.xaml` (row 0 header card)
- ViewModel: `KanbanBoardViewModel` (`src/FocusBot.App.ViewModels/KanbanBoardViewModel.cs`)
- Data source: `IDailyAnalyticsService.GetTodaySummaryAsync` → `DailyFocusSummary`

## ViewModel Surface

The header binds to these properties on `KanbanBoardViewModel`:

- `HasTodayAnalytics` – whether a daily summary is available for the current local day.
- `TodayFocusScoreBucket` – bucketed score 0–10 derived from focused ratio.
- `TodayFocusedTimeText` – formatted focused duration for today (`hh:mm:ss`).
- `TodayDistractedTimeText` – formatted distracted duration for today (`hh:mm:ss`).
- `TodayDistractionCount` – number of distraction events today.
- `TodayAverageDistractionCostText` – formatted average distraction duration, or `—` when not applicable.
- `TodayDateLabel` – formatted date label (`ddd, MMM d`, for example `Fri, Mar 13`).
- `TodayFocusedPercent`, `TodayUnclearPercent`, `TodayDistractedPercent` – relative shares of focused/unclear/distracted time used to drive the segmented bar.
- `ShowTodayFocusScoreChip` – `true` only when analytics exist, AI is configured, and `TodayFocusScoreBucket > 0`.

All of these are populated (or reset) inside `RefreshTodaySummaryAsync`, which calls `IDailyAnalyticsService.GetTodaySummaryAsync(DateTime.Now)` and maps from `DailyFocusSummary` to the header properties.

## Header Layout and Behavior

The header is a card-style `Border` at the top of `KanbanBoardPage` with two logical areas:

- **Context column (left)**
  - Title: `"Today's Focus Analytics"`.
  - Date: `TodayDateLabel` shown using secondary text style.
  - Score chip: when `ShowTodayFocusScoreChip` is `true`, shows `Focus score {TodayFocusScoreBucket}/10` inside a pill using `FbAlignedAccentBrush` and `FbTextOnAccentBrush`.

- **Metrics + balance column (right)**
  - Three metric tiles (each visible only when `HasTodayAnalytics`):
    - **Focused** – clock icon tinted with `FbAlignedAccentBrush` and large `TodayFocusedTimeText`.
    - **Distracted** – warning icon tinted with `FbMisalignedAccentBrush` and large `TodayDistractedTimeText`.
    - **Distractions** – impact icon tinted with `FbNeutralAccentBrush`, large `TodayDistractionCount`, and caption `Avg cost {TodayAverageDistractionCostText}`.
  - **Empty text** – when `HasTodayAnalytics == false`, the metrics and bar are hidden and a single line shows: `No analytics for today yet. Start a task to begin tracking.`
  - **Segmented balance bar** – a thin `Grid` with three columns:
    - Column widths bound to `TodayFocusedPercent`, `TodayUnclearPercent`, `TodayDistractedPercent`.
    - Uses `PercentToGridLengthConverter` to convert these doubles into `GridLength` star values.
    - Segment brushes: `FbAlignedAccentBrush` (focused), `FbNeutralAccentBrush` (unclear), `FbMisalignedAccentBrush` (distracted).

The card itself is always visible; internal content switches between empty state and metric view, so the top-of-board layout remains stable even when there is no data.

## Converters and Resources

- `PercentToGridLengthConverter` (`src/FocusBot.App/Converters/PercentToGridLengthConverter.cs`)
  - Converts `double` → `GridLength` with `GridUnitType.Star`, returning zero-star when the value is not positive.
  - Registered in `App.xaml` as `PercentToGridLengthConverter` and used only for the daily analytics segmented bar.
- Existing theme brushes and styles from `.cursor/rules/focusbot-design.mdc` and `Themes/*.xaml` are reused; no new standalone colors are introduced.

## Testing Notes

The header behavior is validated indirectly via `KanbanBoardViewModel` tests in `TodaySummaryShould`:

- When a summary is available:
  - `HasTodayAnalytics` is `true`, time strings are correctly formatted, and percentages reflect the focused vs distracted ratio.
  - `TodayDateLabel` is set from `AnalyticsDateLocal`.
- When no summary exists:
  - `HasTodayAnalytics` is `false`, time strings are reset to zero, average cost is `—`, date label is empty, and all percentages are zero.

Any future UI changes to the header should keep these contracts intact or update both this document and the tests accordingly.

