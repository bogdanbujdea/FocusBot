# Add Task Popup

## Overview

The Add Task UI is a **light-dismiss popup** (WinUI 3 `Popup` with `IsLightDismissEnabled`). It opens when the user clicks "+ Add Task" and shows two inputs: **description** (required) and **context** (optional).

## Behavior

- **Open:** Click "+ Add Task" → popup appears below the button with description and context fields.
- **Close on outside click:** Clicking or tapping outside the popup closes it (light dismiss). Cancel button also closes it.
- **Draft preserved:** When the popup is closed by light-dismiss or Cancel, the entered text is **not** cleared. Re-opening "+ Add Task" shows the same draft.
- **Submit:** "Add Task" creates the task, clears the fields, and closes the popup. Only successful submit clears the draft.

Sync is handled in code-behind: `ShowAddTaskInput` drives `Popup.IsOpen`; `Popup.Closed` sets `ShowAddTaskInput = false` so the ViewModel stays in sync when the user dismisses by clicking outside.
