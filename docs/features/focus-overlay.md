# Focus Overlay

## Overview

The Focus Overlay is a circular indicator that stays on top of all windows and shows the current focus status (distracted, neutral, or focused). It displays the focus score percentage when a task is active and is always visible so the user can see their status without switching to the FocusBot window. Hovering over the overlay reveals a pause/play button to pause or resume the active task.

**User-facing description:**
> A 96px semi-transparent circle appears in the corner of your screen when FocusBot is running. It stays on top of other windows and can be dragged to reposition. The color changes based on your focus status: green when focused, purple when neutral, and orange when distracted. When you have an active task, your focus score percentage is shown inside the circle. Hover over the circle to reveal a pause/play button that lets you temporarily pause your task (stops time tracking, window monitoring, and AI classification). Click the circle when no task is active to bring the main window to the foreground.

## How It Works

### When and Where

- **When:** The overlay is created and shown as soon as the app launches, right after the main window is activated.
- **Where:** It is positioned in the bottom-right of the primary screen's work area (above the taskbar), with a 16 px margin. The position is computed using `SystemParametersInfo(SPI_GETWORKAREA)`.

### Behavior

- **Topmost:** The window has the `WS_EX_TOPMOST` style so it stays above other windows.
- **No title bar:** It is a popup with no caption, min/max/close buttons, or border.
- **Draggable:** The user can drag it by clicking and moving the mouse.
- **Hidden from taskbar:** The `WS_EX_TOOLWINDOW` style keeps it out of the taskbar.
- **70% Opacity:** Uses `WS_EX_LAYERED` with `SetLayeredWindowAttributes` for transparency.
- **Dynamic colors:** Changes color based on focus status:
  - **Focused (score ≥ 6):** Green `#22C55E`
  - **Neutral (score 4-5):** Purple `#8B5CF6`
  - **Distracted (score < 4):** Orange `#F97316`
- **Score display:** Shows the focus score percentage (0-100) centered in the circle when a task is active and running.
- **Pause indicator:** When a task is paused, the overlay shows a pause icon (⏸) instead of the score, so you know at a glance the task is paused.
- **Hover controls:** When the user hovers over the overlay and a task is active:
  - **When running:** Hover shows pause icon (⏸); click to pause the task.
  - **When paused:** Hover shows play icon (▶); click to resume the task.
- **Click to activate:** Clicking the overlay (outside the button area) brings the main window to the foreground.
- **Real-time updates:** The overlay updates every second as the focus score changes.
- **Status change glow:** When the focus status changes (e.g., distracted → focused), the overlay becomes fully opaque and displays a glowing ring effect for 3 seconds to draw attention.

### Implementation

The overlay is a **pure Win32 layered window** (not a WinUI 3 window) using `UpdateLayeredWindow` with per-pixel alpha for smooth anti-aliased edges:

1. **Window class:** A custom class is registered with a null background brush and a custom `WndProc`.
2. **Window creation:** `CreateWindowEx` with `WS_POPUP` and `WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`. Window is 112×112 pixels to accommodate the glow effect (8px padding around the 96px circle).
3. **Anti-aliased rendering:** Uses GDI+ with `SmoothingMode.AntiAlias` to draw to a 32-bit ARGB bitmap (`Format32bppPArgb` with premultiplied alpha).
4. **Per-pixel transparency:** `UpdateLayeredWindow` with `ULW_ALPHA` composites the bitmap with per-pixel alpha, producing smooth circular edges.
5. **Opacity control:** `BLENDFUNCTION.SourceConstantAlpha` controls overall opacity (179 = 70% normal, 255 = full during highlight).
6. **Painting:** The bitmap is rendered with the current status color circle, glow rings (when highlighted), and score text or pause/play icon, then passed to `UpdateLayeredWindow`.
7. **Hover detection:** `WM_MOUSEMOVE`, `WM_MOUSELEAVE`, and `TrackMouseEvent` detect when the mouse enters/leaves to show/hide the pause/play button.
8. **Pause/play callback:** `OnPausePlayClicked` action is invoked when the button is clicked, allowing the ViewModel to toggle classification.
9. **State updates:** `KanbanBoardViewModel` raises `FocusOverlayStateChanged` events which trigger `UpdateLayeredBitmap()` to re-render.
10. **Highlight timer:** A `System.Threading.Timer` restores normal opacity after 3 seconds when status changes.

## Architecture

- **App:** `App.xaml.cs` creates `FocusOverlayWindow` in `OnLaunched`, passing `INavigationService` and a pause/play callback, and subscribes to `FocusOverlayStateChanged` from `KanbanBoardViewModel`.
- **Views:** `FocusOverlayWindow` in `FocusBot.App/Views/FocusOverlayWindow.cs` implements the Win32 window with `UpdateState()` method for dynamic updates and hover state tracking.
- **Events:** `FocusOverlayStateChangedEventArgs` in `FocusBot.Core/Events` carries active task state, score, status, and `IsTaskPaused` flag.
- **Navigation:** `INavigationService.ActivateMainWindow()` brings the main window to foreground when the overlay is clicked with no active task.
- **Task control:** `KanbanBoardViewModel` exposes `ToggleTaskPause()` which is called via the overlay's pause/play button callback.

## Future Enhancements

- **Context menu:** Right-click to hide, show settings, or close.
- **Persist position:** Save and restore overlay position across sessions.
