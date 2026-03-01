# Focus Overlay

## Overview

The Focus Overlay is a circular indicator that stays on top of all windows and shows the current focus status (distracted, neutral, or focused). It displays the focus score percentage when a task is active and is always visible so the user can see their status without switching to the FocusBot window.

**User-facing description:**
> A 64px semi-transparent circle appears in the corner of your screen when FocusBot is running. It stays on top of other windows and can be dragged to reposition. The color changes based on your focus status: green when focused, purple when neutral, and orange when distracted. When you have an active task, your focus score percentage is shown inside the circle. Click the circle when no task is active to bring the main window to the foreground.

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
- **Score display:** Shows the focus score percentage (0-100) centered in the circle when a task is active.
- **Click to activate:** Clicking the overlay brings the main window to the foreground.
- **Real-time updates:** The overlay updates every second as the focus score changes.
- **Status change glow:** When the focus status changes (e.g., distracted → focused), the overlay becomes fully opaque and displays a glowing ring effect for 3 seconds to draw attention.

### Implementation

The overlay is a **pure Win32 window** (not a WinUI 3 window) so we can control shape and z-order directly:

1. **Window class:** A custom class is registered with a null background brush (we paint ourselves) and a custom `WndProc`.
2. **Window creation:** `CreateWindowEx` with `WS_POPUP | WS_VISIBLE` and `WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`. Window is 80×80 pixels to accommodate the glow effect (8px padding around the 64px circle).
3. **Circular shape:** `CreateEllipticRgn` and `SetWindowRgn` make the visible area a circle; the region expands temporarily to include glow when highlighted.
4. **Transparency:** `SetLayeredWindowAttributes` with `LWA_ALPHA` set to 179 (70% opacity), or 255 (full) during highlight.
5. **Painting:** GDI+ is used in `WM_PAINT` to fill the circle with the current status color, draw the score text centered, and render the glow effect (concentric semi-transparent rings) when highlighted.
6. **State updates:** `KanbanBoardViewModel` raises `FocusOverlayStateChanged` events which trigger `InvalidateRect` to repaint.
7. **Highlight timer:** A `System.Threading.Timer` restores normal opacity and region after 3 seconds when status changes.

## Architecture

- **App:** `App.xaml.cs` creates `FocusOverlayWindow` in `OnLaunched`, passing `INavigationService`, and subscribes to `FocusOverlayStateChanged` from `KanbanBoardViewModel`.
- **Views:** `FocusOverlayWindow` in `FocusBot.App/Views/FocusOverlayWindow.cs` implements the Win32 window with `UpdateState()` method for dynamic updates.
- **Events:** `FocusOverlayStateChangedEventArgs` in `FocusBot.Core/Events` carries active task state, score, and status.
- **Navigation:** `INavigationService.ActivateMainWindow()` brings the main window to foreground when the overlay is clicked with no active task.

## Future Enhancements

- **Anti-aliased edges:** Use `UpdateLayeredWindow` with a 32-bit ARGB bitmap for smooth circular edges.
- **Context menu:** Right-click to hide, show settings, or close.
- **Persist position:** Save and restore overlay position across sessions.
