# Focus Bot - App Documentation

## App Identity & Store Properties

| Property | Value |
|----------|-------|
| **Display Name** | Focus Bot |
| **Publisher** | Bogdan Bujdea |
| **Publisher Identity** | `CN=4E87F883-6FEA-4B8F-9088-5E5336A782BD` |
| **Version** | 1.0.0.0 |
| **Target Device Family** | Windows Desktop |
| **Minimum OS Version** | Windows 10 1809 (10.0.17763.0) |
| **Package Type** | MSIX (single-project packaging) |
| **Capability** | `runFullTrust` (required for Win32 window monitoring) |

### Store Subscription Add-on

| Property | Value |
|----------|-------|
| **Add-on Title** | FocusBot Pro |
| **Product ID** | `focusbot.subscription.monthly` |
| **Store ID** | `9P6D9DGTVXLR` |
| **Period** | Monthly, auto-renewing |
| **Price** | $4.99 USD |
| **Keywords** | focus, productivity, AI, subscription, pro |

---

## App Overview

Focus Bot is a Windows desktop productivity application that helps users stay focused on their tasks. It combines a Kanban-style task board with real-time foreground window monitoring and AI-powered classification to produce a live Focus Score, a percentage (0-100%) showing how well the user is staying on track.

The app monitors which applications and browser tabs the user switches to while working on a task, sends that context to an AI provider for classification, and displays immediate feedback: Focused, Unclear, or Distracted, along with a reason from the AI.

### Target Audience

| Segment | Description |
|---------|-------------|
| **Knowledge workers** | Professionals who multitask across many apps on Windows |
| **Developers** | Technical users who already have API keys from AI providers |
| **Productivity-minded professionals** | Users willing to pay for a managed experience without API key setup |

---

## Features

### Kanban Task Board
A three-column board (To Do, In Progress, Done) for organizing work. Supports adding, editing, deleting, and dragging tasks between columns. Only one task can be In Progress at a time; starting a new task automatically moves the previous one back to To Do.

### Real-Time Focus Status Bar
When a task is In Progress, a status bar appears above the board showing the current foreground application and window title, along with the AI classification result:

| Status | Score Range | Color | Icon |
|--------|-------------|-------|------|
| **Focused** | 6-10 | Green | Fire |
| **Unclear** | 4-5 | Purple | Question mark |
| **Distracted** | 1-3 | Orange | Warning |

Each status includes a reason from the AI explaining why the current window is or isn't relevant to the task.

### Focus Score
A time-weighted average of all alignment scores during a work session, expressed as a percentage from 0% to 100%. The score updates live every second and is saved when a task moves to Done.

**Calculation:**
```
Focus Score = (Sum of (AlignmentScore x DurationSeconds)) / TotalSeconds x 10
```

For example, 30 minutes in VS Code (score 9) and 10 minutes in social media (score 2) produces a Focus Score of 72.5%.

### Smart Classification Caching
Each window context (process name + window title) is hashed using SHA256. Once the AI classifies a window, the result is cached so repeated visits to the same window don't trigger additional API calls.

### Idle Detection
Tracking pauses automatically when the user is idle (no keyboard or mouse input for 5 minutes) and resumes when activity is detected. Idle time is not counted toward the Focus Score.

### Task Context Hints
Users can provide optional context when creating or editing a task (e.g., "Outlook is work email", "Slack messages are for standup coordination"). The AI uses these hints as authoritative guidance to improve classification accuracy.

### Multi-Provider AI Support
Focus Bot supports multiple AI providers through the [LlmTornado](https://github.com/lofcz/LlmTornado) NuGet package:

| Provider | Example Models |
|----------|---------------|
| **OpenAI** | gpt-4o-mini, gpt-4.1-mini, gpt-5-nano |
| **Anthropic** | Claude Opus, Claude Sonnet, Claude Haiku |
| **Google** | Gemini 2.5 Flash, Gemini 2.5 Flash Lite |

The default provider is OpenAI with the gpt-4o-mini model. Users can switch providers and models in the Settings page. LlmTornado provides a unified API surface, so adding new providers in the future requires minimal code changes.

### Encrypted API Key Storage
API keys are encrypted using Windows DPAPI (Data Protection API) before being stored locally in a JSON settings file. Keys are decrypted only when needed for AI requests.

### Fluent Design with Theme Support
The app follows Windows Fluent Design guidelines with full support for:
- **Dark mode** (default, deep purple palette)
- **Light mode**
- **High Contrast mode** (solid colors, no transparency)

### 100% Local Data
All task data, focus scores, and classification history are stored locally in a SQLite database on the user's device. There is no cloud sync, no telemetry, and no data sharing beyond the AI classification requests themselves.

---

## How It Works

### 1. Task Management
Users create tasks on the Kanban board, stored in a local SQLite database. Each task has a title, an optional description, and optional context hints for the AI. Tasks track total elapsed time and a final Focus Score when completed.

### 2. Window Monitoring (Win32 APIs)
When a task enters In Progress, Focus Bot begins monitoring the foreground window using Win32 APIs from `user32.dll`:

| API | Purpose |
|-----|---------|
| `GetForegroundWindow` | Gets the handle of the currently active window |
| `GetWindowText` | Retrieves the window title bar text |
| `GetWindowThreadProcessId` | Gets the process ID that owns the window, used to resolve the process name |
| `GetLastInputInfo` | Gets the timestamp of the last keyboard or mouse input, used for idle detection |

The monitor polls every 1 second and compares the current foreground window to the previous state. When a change is detected, it raises an event with the new process name and window title. System processes (like SearchHost and ShellExperienceHost) and Focus Bot's own window are filtered out.

Browser tab changes are also detected through title polling, since browsers update their window title when the active tab changes.

### 3. AI Classification
Each window context (process name + title + task description + user hints) is sent to the configured AI provider via LlmTornado. The AI returns a JSON response with a score (1-10) and a reason. The system prompt instructs the AI to act as a focus alignment classifier, evaluating how relevant the current application is to the user's task.

Results are cached by a SHA256 hash of the process name and window title, so switching back to a previously classified window is instant and free.

### 4. Focus Score Calculation
Time segments are aggregated by task, context hash, and alignment score to prevent database bloat. The focus score is recalculated every second as a time-weighted average, persisted to the database every 5 seconds, and finalized when the task moves to Done.

---

## Pricing Model

### Tiers

| Tier | Price | Description |
|------|-------|-------------|
| **Free (Bring Your Own Key)** | $0/month | User provides their own API key from OpenAI, Anthropic, or Google. Full features, unlimited usage. |
| **FocusBot Pro** | $4.99/month | No API key needed. Managed through the Windows Store as a subscription add-on. Cancel anytime. |

### Cost Analysis

The default model (gpt-4o-mini) costs approximately $0.0001 per classification call. A typical user making ~1,000 classifications per month incurs about $0.10 in API costs. Even heavy users (~6,000 calls/month) only cost about $0.60.

With the Windows Store taking a 15% cut (for apps earning under $25M/year), the net revenue per Pro subscriber is approximately $3.65/month after the Store cut and API costs.

### Implementation Phases

| Phase | Status | Description |
|-------|--------|-------------|
| **Phase 1: Mode Selection** | Implemented | Settings UI with BYOK vs Subscription radio buttons |
| **Phase 2: Store Integration** | Planned | Windows Store subscription purchase flow via `Windows.Services.Store` APIs |
| **Phase 3: Managed Key** | Planned | Embedded API key for subscribers (with obfuscation; server proxy planned when revenue exceeds $500/month) |
| **Phase 4: Store Submission** | Planned | Partner Center setup, add-on creation, WACK testing, and app submission |

### Future Pricing Options
- Annual subscription at $49.99/year (2 months free)
- Regional pricing for developing markets
- 7-day free trial for the Pro tier

---

## Design Language

### Color Palette

**Dark Mode (default):**

| Role | Color |
|------|-------|
| Page background | `#110E1A` (deep purple) |
| Column background | `#1C1730` |
| Card background | `#2A2242` |
| Card border | `#3A3058` |
| Primary accent | `#A78BFA` (violet) |
| Focused/aligned | `#4ADE80` (green) |
| Distracted/misaligned | `#FB923C` (orange) |
| Unclear/neutral | violet (primary accent) |

**Light Mode:**

| Role | Color |
|------|-------|
| Page background | `#EDE9F5` |
| Card background | `#FFFFFF` |
| Primary accent | `#7C3AED` |
| Focused/aligned | `#22C55E` |

### Typography
- **Font:** Segoe UI Variable (WinUI 3 system font)
- Column headers: 12px SemiBold, uppercase, letter-spacing 80
- Task titles: 14px Medium
- Secondary text: 11px Regular

### Visual Hierarchy
Three elevation layers (Page < Column < Card) with progressive lightening. Cards use a 3px left accent bar for state indication: violet for To Do, green for In Progress, muted green for Done.

### Materials
Desktop Acrylic backdrop on the window, Acrylic brushes on surfaces with ~65-70% tint opacity. High Contrast mode disables all transparency and uses solid system colors.

### Accessibility
- Color is never the sole indicator of state; always paired with icons, text labels, or accent bars
- All interactive elements have `AutomationProperties.Name` for screen readers
- Full keyboard navigation support (Tab, Enter, arrow keys)
- High Contrast theme validated alongside Dark and Light modes

---

## Store Submission Requirements

### Before Submission

- [ ] **Privacy Policy URL** - Must be hosted at a publicly accessible URL. Must disclose that window titles and task descriptions are sent to AI providers for classification, that subscription management is handled via the Microsoft Store, and that no activity data is stored on any server.
- [ ] **WACK Testing** - The app must pass the Windows App Certification Kit before submission.
- [ ] **Screenshots** - At least one required; recommended at least 4. Resolution: 1366x768 or larger (supports up to 3840x2160). PNG format. Suggested screenshots:
  1. Kanban board with tasks in all three columns
  2. Focus Status Bar showing a Focused classification with a reason
  3. Settings page showing both pricing modes (BYOK and Subscription)
  4. Add or Edit task popup with context hints
- [ ] **Store Description** - The description, product features, and release notes for the listing page.
- [ ] **Subscription Add-on** - Created in Partner Center with the product ID `focusbot.subscription.monthly`.

### Age Rating
- Uses online services (AI classification)
- No user-generated content displayed to other users
- No physical goods purchases

### Certification Test Matrix

| Scenario | Own Key Mode | Pro (Subscribed) | Pro (Not Subscribed) |
|----------|-------------|-----------------|---------------------|
| AI classification works | Yes | Yes | No (shows subscribe prompt) |
| Settings UI accessible | Yes | Yes | Yes |
| Mode selection persists | Yes | Yes | Yes |
| Focus tracking works | Yes | Yes | No |

### Theme Testing
The app must be tested and function correctly in all three themes:
- Dark mode
- Light mode
- High Contrast mode

### Scale Testing
The app must be usable at 100% and 200% display scaling.

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| **Runtime** | .NET 10 |
| **UI Framework** | WinUI 3 (Windows App SDK 1.8) |
| **Database** | SQLite with Entity Framework Core 10 |
| **AI Integration** | [LlmTornado](https://github.com/lofcz/LlmTornado) (multi-provider: OpenAI, Anthropic, Google) |
| **MVVM** | CommunityToolkit.Mvvm |
| **Result Types** | CSharpFunctionalExtensions |
| **Encryption** | Windows DPAPI (via Microsoft.AspNetCore.DataProtection) |
| **Packaging** | MSIX (single-project) |
| **Testing** | xUnit, Moq, EF Core InMemory |

### Architecture

The solution follows Clean Architecture with four layers:

```
FocusBot.Core          - Entities, interfaces, domain logic (no Windows dependencies)
FocusBot.Infrastructure - Win32 services, EF Core, AI integration, Store APIs
FocusBot.App.ViewModels - MVVM ViewModels (no Windows dependencies)
FocusBot.App            - WinUI 3 views, XAML, app entry point
```

Dependencies flow inward: App depends on ViewModels, which depend on Core. Infrastructure implements Core interfaces and is wired up via dependency injection in the App layer.
