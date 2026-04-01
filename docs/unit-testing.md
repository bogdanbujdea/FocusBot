# Unit Testing Guide

This document describes how we write unit tests in Foqus: structure, naming, Roy Osherove's principles, and the Awesome Assertions library.

## Test Structure (Per Method)

Tests are organized **per method or per behavior**, not per class. One file per method/behavior, inside a folder named after the type under test.

```
tests/
├── FocusBot.Core.Tests/
│   └── Entities/
│       └── UserTaskTests/
│           ├── NewTaskShould.cs          # behavior: new entity defaults
│           └── IsActiveShould.cs         # behavior: IsActive property
│
└── FocusBot.Infrastructure.Tests/
    └── Data/
        ├── TaskRepositoryTestBase.cs     # shared in-memory DB setup
        └── TaskRepositoryTests/
            ├── AddTaskAsyncShould.cs
            ├── GetByIdAsyncShould.cs
            ├── SetActiveAsyncAndSetCompletedAsyncShould.cs
            └── ...
```

- **Folder**: Name of the type under test (e.g. `TaskRepositoryTests`, `UserTaskTests`).
- **File**: `MethodNameShould.cs` or `BehaviorNameShould.cs` (e.g. `GetByIdAsyncShould.cs`, `NewTaskShould.cs`).
- **Class**: Same as file name without extension; holds all tests for that one method/behavior.

## Naming Conventions

### Test class name

- **`MethodNameShould`** for a single method: `AddTaskAsyncShould`, `GetInProgressTaskAsyncShould`.
- **`BehaviorNameShould`** for a behavior that spans properties or defaults: `NewTaskShould`, `IsActiveShould`.

### Test method name

One clear outcome per test. Name describes **what is being tested** and **the expected result**.

Pattern: **`Result_WhenCondition`** or **`DoSomething_WhenCondition`**

Good:

- `ReturnNull_WhenIdDoesNotExist`
- `ReturnTask_WhenIdExists`
- `SetActiveAsync_MakesTaskActive`
- `SetActiveAsync_MovesPreviousActiveTaskToCompleted`
- `HaveNonEmptyTaskId`
- `ReturnTrue_WhenNotCompleted`

Avoid:

- `TestGetById`
- `GetById_Test`
- Vague names that don't say what should happen or when.

When a test fails, the name should tell you **what behavior** broke, not just which method was called.

## Structure of a Unit Test (AAA)

Every test has three parts: **Arrange**, **Act**, **Assert**. No logic (no `if`, loops, or complex expressions) in the test.

**Rule:** Every test method must explicitly mark these three sections with comments: `// Arrange`, `// Act`, and `// Assert`. This keeps the AAA structure visible and consistent across all unit tests.

```csharp
[Fact]
public async Task ReturnTask_WhenIdExists()
{
    // Arrange
    var created = await Repository.AddTaskAsync("Find me");

    // Act
    var found = await Repository.GetByIdAsync(created.TaskId);

    // Assert
    found.Should().NotBeNull();
    found!.TaskId.Should().Be(created.TaskId);
    found.Description.Should().Be("Find me");
}
```

- **Arrange**: Set up data and dependencies (e.g. create task, set up mocks).
- **Act**: Call the single unit of work (one method or one logical operation).
- **Assert**: Check outcomes with Awesome Assertions (see below).

Keep tests boring: no conditionals, no loops. If you need many scenarios, add more tests.

## Roy Osherove Principles

### 1. One unit of behavior per test

A **unit** is one logical behavior with **one reason to fail**. Not one class, not one method with five concerns.

- **Bad**: One test that asserts description, status, id format, and timestamps. When it fails, you don't know which concern broke.
- **Good**: Separate tests for “description is set”, “status is ToDo”, “id is parseable GUID”. Each test fails for one clear reason.

Split tests so that a failing test points to a single broken behavior.

### 2. Tests must be deterministic

Same inputs and setup must give the same result every time. No dependence on:

- Time (e.g. “now”)
- Randomness
- Threading
- Network, file system, or environment

**Bad**: Assert `task.CreatedAt` is “near `DateTime.UtcNow`” (flaky).

**Good**: Assert `task.CreatedAt` is not default and `task.CreatedAt.Kind == DateTimeKind.Utc` (deterministic).

Use fakes for time, randomness, and external services so tests stay deterministic.

### 3. Tests should be fast

Unit tests should run in milliseconds. If they are slow, people skip them and feedback loops suffer.

- Run unit tests on every save or commit.
- Put slower, broader checks in integration tests and run them less often.

### 4. Tests should be isolated

A test must not depend on:

- Another test
- Execution order
- Shared mutable state
- Real external systems

Use a fresh in-memory database (or new GUID) per test class so tests don’t leak state. If you need shared setup, use a base class or helper that creates a **new** context per test.

### 5. Tests should be self-describing

You should understand **what is being tested** and **why it failed** from the test name and structure alone, without debugging.

- Descriptive test names (see Naming Conventions).
- Clear AAA structure.
- Assertions that read like sentences (e.g. `found.Should().BeNull()`).

### 6. Test behavior, not implementation

Assert **inputs, outputs, and observable side effects**. Do not assert:

- Private methods
- Internal call order or exact algorithm steps

That keeps tests stable when you refactor implementation. If behavior is the same, tests stay green.

### 7. Avoid logic in tests

Tests should be trivial. No `if`, loops, or non-trivial calculations. If the test has logic, it can be wrong and you lose trust: “Is the bug in production code or in the test?”

### 8. A failing test should tell you what broke

When a test fails, you should quickly know whether the bug is in production code or in the test (Osherove’s “trust rule”). Clear names, one behavior per test, and good assertion messages (see Awesome Assertions) make that possible.

**Definition (The Art of Unit Testing):**  
“A unit test is an automated piece of code that invokes a single unit of work and checks its behavior **independently** from other units.”  
Independence (isolation, no shared state, no ordering) is the key.

## Awesome Assertions

We use [Awesome Assertions](https://awesomeassertions.org/) (community fork of FluentAssertions) for all assertions. Add the package and a global `using AwesomeAssertions;` so `.Should()` is available everywhere.

### Why Awesome Assertions

- **Readable**: `found.Should().BeNull()` reads like a sentence.
- **Better failure messages**: Tells you expected vs actual (e.g. “Expected null, but found UserTask with Description = …”).
- **Fluent**: Chain assertions when they describe one outcome: `.Should().HaveCount(2).And.OnlyContain(...)`.

### Common patterns

**Null and equality**

```csharp
task.Should().NotBeNull();
task!.Description.Should().Be("Ship the feature");
task.IsCompleted.Should().BeFalse();
found.Should().BeNull();
```

**Booleans**

```csharp
task.IsActive.Should().BeTrue();
Guid.TryParse(task.TaskId, out _).Should().BeTrue();
```

**Collections**

```csharp
toDo.Should().BeEmpty();
toDo.Should().HaveCount(3);
completed.Should().OnlyContain(t => t.IsCompleted);
```

**Comparisons**

```csharp
toDo[0].CreatedAt.Should().BeAfter(toDo[1].CreatedAt);
task.CreatedAt.Should().NotBe(default);
task.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
```

**Exceptions (async)**

```csharp
var act = async () => await Repository.DeleteTaskAsync(Guid.NewGuid().ToString());
await act.Should().NotThrowAsync();
```

**Avoid** wrapping in `Assert.*`; use `.Should()` only. Prefer one logical assertion per test (or a short chain that expresses one outcome).

## Testing Stack

### C# (.NET)

| Component | Technology |
|---|---|
| Test framework | xUnit |
| Assertions | Awesome Assertions (`.Should()`) |
| In-memory DB | Microsoft.EntityFrameworkCore.InMemory |
| Mocking | Moq (when needed) |
| Integration | WebApplicationFactory + CustomWebApplicationFactory (test JWT) |

### TypeScript (Extension, Web App)

| Component | Technology |
|---|---|
| Test framework | Vitest |
| Assertions | Vitest `expect` + `@testing-library/jest-dom` |
| DOM environment | jsdom |
| Component testing | @testing-library/react + @testing-library/user-event |
| Mocking | `vi.mock()`, `vi.hoisted()`, `vi.fn()` |

## Running Tests

```powershell
# C# — individual projects (no .sln test)
dotnet test tests/FocusBot.Core.Tests/FocusBot.Core.Tests.csproj
dotnet test tests/FocusBot.WebAPI.Tests/FocusBot.WebAPI.Tests.csproj
dotnet test tests/FocusBot.WebAPI.IntegrationTests/FocusBot.WebAPI.IntegrationTests.csproj
dotnet test tests/FocusBot.App.ViewModels.Tests/FocusBot.App.ViewModels.Tests.csproj   # Windows only
dotnet test tests/FocusBot.Infrastructure.Tests/FocusBot.Infrastructure.Tests.csproj     # Windows only

# TypeScript — extension
cd browser-extension && npm test

# TypeScript — web app
cd src/foqus-web-app && npm test

# Watch mode (Vitest)
cd browser-extension && npm run test:watch
cd src/foqus-web-app && npm run test:watch
```

### Test Project Summary

| Project | ~Tests | Platform | Coverage |
|---|---|---|---|
| `FocusBot.Core.Tests` | 25 | Cross-platform | Entity behavior, domain logic |
| `FocusBot.WebAPI.Tests` | 82 | Cross-platform | Service logic, webhook idempotency, security |
| `FocusBot.WebAPI.IntegrationTests` | 32 | Cross-platform | Full HTTP pipeline, InMemory DB, test JWT |
| `FocusBot.App.ViewModels.Tests` | ~20 | Windows only | ViewModel behavior |
| `FocusBot.Infrastructure.Tests` | ~10 | Windows only | Data access, Win32 services |
| `browser-extension/tests/` | 83 | Cross-platform | Analytics, API client, storage, metrics, utils |
| `foqus-web-app/src/**/*.test.*` | 58 | Cross-platform | Components, pages, API client, utils |

## What to Test

- **Do**: Business rules, repository CRUD and status transitions, entity defaults and computed properties, error paths (e.g. not found).
- **Don’t**: Private implementation, exact steps of an algorithm, or trivial getters/setters with no logic.
- **Prefer**: One behavior per test, deterministic setup, and assertions that document the expected behavior.
---

## TypeScript / Vitest Testing

The same principles apply to TypeScript tests (one behavior per test, deterministic, isolated, AAA structure). The key differences are:

### Structure

- **Browser extension**: Tests in `browser-extension/tests/` as `*.test.ts` files. One file per module.
- **Web app**: Tests co-located with source as `*.test.ts` / `*.test.tsx`. One test file per component/module.
- **Test setup**: Web app uses `src/test/setup.ts` for global mocks (Supabase) and `@testing-library/jest-dom` matchers.

### Naming

- Test files: `moduleName.test.ts` or `ComponentName.test.tsx`
- `describe` blocks: group by function or component
- `it` / `test`: describe outcome clearly

```typescript
describe("formatDuration", () => {
  it("formats seconds as mm:ss for durations under 1 hour", () => {
    expect(formatDuration(125)).toBe("02:05");
  });
});
```

### Component Testing

Use `@testing-library/react` with `@testing-library/user-event`:

```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";

describe("SessionTimer", () => {
  it("displays elapsed time excluding paused seconds", () => {
    render(<SessionTimer startedAtIso="..." totalPausedSeconds={60} nowMs={fixedNow} />);
    expect(screen.getByText("04:00")).toBeInTheDocument();
  });
});
```

### Mocking

```typescript
// Module mock
vi.mock("../auth/supabase", () => ({
  supabase: { auth: { getSession: vi.fn().mockResolvedValue({ data: { session: mockSession } }) } }
}));

// Hoisted mock (for imports that run at module load)
const mockApi = vi.hoisted(() => ({
  getActiveSession: vi.fn(),
  startSession: vi.fn(),
}));
vi.mock("../api/client", () => ({ api: mockApi }));
```

### AAA in TypeScript

The same three-section approach; comments optional but encouraged for complex tests:

```typescript
it("calls startSession with task title", async () => {
  // Arrange
  mockApi.startSession.mockResolvedValue({ ok: true, data: { sessionId: "1" } });
  render(<DashboardPage />);

  // Act
  await userEvent.type(screen.getByRole("textbox"), "Write docs");
  await userEvent.click(screen.getByRole("button", { name: /start/i }));

  // Assert
  expect(mockApi.startSession).toHaveBeenCalledWith(
    expect.objectContaining({ sessionTitle: "Write docs" })
  );
});
```
## Example: One Behavior Per Test

**Bad** (multiple behaviors, one test):

```csharp
[Fact]
public async Task AddTaskAsync_Works()
{
    var task = await Repository.AddTaskAsync("Do stuff");
    Assert.NotNull(task);
    Assert.Equal("Do stuff", task.Description);
    task.IsCompleted.Should().BeFalse();
    Assert.True(Guid.TryParse(task.TaskId, out _));
}
```

**Good** (one reason to fail per test):

```csharp
// AddTaskAsyncShould.cs

[Fact]
public async Task CreateActiveTaskWithDescription()
{
    var task = await Repository.AddTaskAsync("Ship the feature");
    task.Should().NotBeNull();
    task!.Description.Should().Be("Ship the feature");
    task.IsCompleted.Should().BeFalse();
    Guid.TryParse(task.TaskId, out _).Should().BeTrue();
}
```

Each test has a single focus. When “CreateActiveTaskWithDescription” fails, you know the problem is with description, active state, or id.

## Summary

- **Structure**: Per-method/behavior files under a folder named after the type (`MethodNameShould.cs`, `BehaviorNameShould.cs`).
- **Naming**: Test methods describe outcome and condition (`Result_WhenCondition`).
- **AAA**: Arrange, Act, Assert; every test must use `// Arrange`, `// Act`, `// Assert` comments; no logic in tests.
- **Osherove**: One behavior per test, deterministic, fast, isolated, self-describing, behavior-focused, no logic, failing test = clear failure.
- **Assertions**: Use Awesome Assertions (`.Should()`) for readability and better failure messages.
