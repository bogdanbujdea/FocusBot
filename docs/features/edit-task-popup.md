# Edit Task Popup

## Overview

The Edit Task UI is a **light-dismiss popup** (WinUI 3 `Popup` with `IsLightDismissEnabled`). It opens when the user clicks "Edit" on a task card and shows two inputs: **description** (required) and **context** (optional), pre-filled with the current task values.

## Behavior

- **Open:** Click "Edit" on any task card (To Do, In Progress, or Done) → popup appears anchored to the edit button with current description and context pre-filled.
- **Close on outside click:** Clicking or tapping outside the popup closes it (light dismiss). Cancel button also closes it.
- **Draft preserved:** When the popup is closed by light-dismiss or Cancel, the entered text is **not** cleared. Re-opening Edit (on any task) replaces the draft with that task's current values.
- **Submit:** "Save" updates the task's description and context, clears the draft fields, and closes the popup. The board refreshes to show updated values on the card.

## Technical Details

- Popup sync is handled in code-behind: `ShowEditTaskInput` drives `Popup.IsOpen`; `Popup.Closed` sets `ShowEditTaskInput = false` so the ViewModel stays in sync when the user dismisses by clicking outside.
- Placement target is set dynamically to the Edit button that was clicked, so the popup appears near the relevant card.
- The `BeginEditTaskCommand` loads the task from the repository and populates `EditTaskDescription` and `EditTaskContext`.
- The `SaveEditTaskCommand` calls `UpdateTaskAsync` to persist both description and context in a single operation.
