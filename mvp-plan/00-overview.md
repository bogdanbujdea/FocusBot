# FocusBot WinUI MVP Plan

Build FocusBot incrementally, with each phase delivering a complete user-facing feature. At the end of each phase, you have a working app with that feature functional.

## Phases

1. [Task Management (Kanban Board)](01-task-management.md)
2. [Window Monitoring](02-window-monitoring.md)
3. [OpenAI Setup + AI Alignment Classification](03-ai-classification.md)
4. [Time Tracking](04-time-tracking.md)
5. [Focus Score](05-focus-score.md)
6. [Store Submission](06-store-submission.md)

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 |
| UI | WinUI 3 (Windows App SDK 1.6+) |
| Database | SQLite with EF Core |
| AI | OpenAI SDK 2.* (gpt-4o-mini) |
| MVVM | CommunityToolkit.Mvvm |
| Logging | Serilog |
| Packaging | MSIX (packaged app) |
| Testing | xUnit, Moq, EF Core InMemory |

## Solution Structure

```
FocusBot/
├── FocusBot.sln
├── src/
│   ├── FocusBot.Core/              # Domain entities and interfaces
│   ├── FocusBot.Infrastructure/    # Data access, services, orchestration
│   └── FocusBot.App/               # WinUI 3 application (packaged)
└── tests/
    ├── FocusBot.Core.Tests/
    └── FocusBot.Infrastructure.Tests/
```

## Reference: deskbot Patterns

| Concern | deskbot File |
|---------|--------------|
| Window monitoring | `Infrastructure/Services/WindowMonitorService.cs` |
| AI classification | `Infrastructure/Services/OpenAIService.cs` |
| Orchestration | `Skills/WindowMonitoring/WindowChangeOrchestrator.cs` |
| Time tracking spec | [time-tracking-spec.md](../deskbot/docs/time-tracking-spec.md) |
| Testing patterns | [testing.md](../deskbot/docs/testing.md) |

## Out of Scope for MVP

- Cross-device sync
- Team/organization features
- Export/import functionality
- Advanced analytics/trends charts
- Multiple concurrent InProgress tasks
- Local process rules (AI-only for now)
