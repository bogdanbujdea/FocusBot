# Focus Page Classification Display

This document defines the current desktop `FocusPage` behavior after the ViewModel cleanup.

## Supported behavior

- If there is no active session, the classification message section is hidden.
- If there is an active session, classification status follows the extension-style model:
  - `Paused` (overrides all other states)
  - `Analyzing page...` when classification is running
  - `Classifier error` when AI classification fails
  - `Aligned (x/10)` for score > 5
  - `Neutral (x/10)` for score = 5
  - `Distracting (x/10)` for score < 5
  - `Waiting for signal` when no result exists yet
- Classification reason text is hidden when paused and otherwise shows the latest available detail (SignalR reason or classifier error text).
- Extension state is session-scoped:
  - Connected: show `Extension connected`.
  - Disconnected: show `Extension disconnected` and links for Edge and Chrome.

## Removed behavior

- Foreground-browser checks for extension promo visibility.
- Legacy focus status control category/checking rendering.
- Focus category text rendering in the compact status control.
