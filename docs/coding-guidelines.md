# Coding Guidelines

This document defines the coding standards for FocusBot. All new code should follow these guidelines, and existing code should be refactored to comply when touched.

---

## Architecture

FocusBot follows **Clean Architecture** with the following layers:

```
FocusBot.Core           <- Domain: Entities, Interfaces, Events (no dependencies)
FocusBot.Infrastructure <- Data access, external services (depends on Core)
FocusBot.App.ViewModels <- Presentation logic (depends on Core)
FocusBot.App            <- UI/XAML (depends on ViewModels, Infrastructure)
```

### Dependency Rules

- **Core** has no dependencies on other FocusBot projects
- **Infrastructure** depends only on Core
- **ViewModels** depend only on Core (interfaces)
- **App** wires everything together via DI

### Dependency Injection

Use **constructor injection** with interfaces:

```csharp
public class OpenAIService : IOpenAIService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(ISettingsService settingsService, ILogger<OpenAIService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }
}
```

**Primary constructors** are acceptable for simple cases:

```csharp
public class TaskRepository(AppDbContext context) : ITaskRepository
{
    // context is available as a field
}
```

---

## C# Style

### File-Scoped Namespaces

Use file-scoped namespaces (single line, no braces):

```csharp
namespace FocusBot.Core.Entities;

public class UserTask { }
```

### Nullable Reference Types

Nullable reference types are **enabled**. Be explicit about nullability:

```csharp
public string Name { get; set; } = string.Empty;  // Never null
public string? OptionalValue { get; set; }         // Can be null
```

### Expression-Bodied Members

Use expression bodies for simple, single-expression methods:

```csharp
public bool IsActive => Status == TaskStatus.InProgress;

private void OpenSettings() => _navigationService.NavigateToSettings();
```

### Braces

Always use braces for multi-line blocks. Single-line statements may omit braces:

```csharp
// OK
if (string.IsNullOrEmpty(taskId))
    return;

// OK
if (string.IsNullOrEmpty(taskId))
{
    _logger.LogWarning("Task ID is empty");
    return;
}
```

---

## Return Types

### No Tuple Returns

**Do not** return tuples from methods. Use named types instead.

**Bad:**

```csharp
private static (string ProcessName, string WindowTitle) GetForegroundWindowInfo()
{
    // ...
    return (processName, title);
}
```

**Good:**

```csharp
private static ForegroundWindowInfo GetForegroundWindowInfo()
{
    // ...
    return new ForegroundWindowInfo(processName, title);
}

private sealed record ForegroundWindowInfo(string ProcessName, string WindowTitle);
```

**Why:** Tuples obscure intent. Named types are self-documenting, refactorable, and provide better IntelliSense.

### No Null Returns - Use Result Pattern

**Do not** return `null` to indicate failure. Use the `Result` type from [CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions).

**Bad:**

```csharp
public async Task<AlignmentResult?> ClassifyAlignmentAsync(...)
{
    if (string.IsNullOrWhiteSpace(apiKey))
        return null;  // Caller doesn't know why

    try
    {
        // ...
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Classification failed");
        return null;  // Error is swallowed
    }
}
```

**Good:**

```csharp
public async Task<Result<AlignmentResult>> ClassifyAlignmentAsync(...)
{
    if (string.IsNullOrWhiteSpace(apiKey))
        return Result.Failure<AlignmentResult>("API key not configured");

    try
    {
        // ...
        return Result.Success(result);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Classification failed");
        return Result.Failure<AlignmentResult>($"Classification failed: {ex.Message}");
    }
}
```

**Caller:**

```csharp
var result = await _openAIService.ClassifyAlignmentAsync(task, process, title);
if (result.IsFailure)
{
    _logger.LogWarning("Alignment failed: {Error}", result.Error);
    return;
}

var alignment = result.Value;
FocusScore = alignment.Score;
```

**Why:** Result forces callers to handle failure explicitly. Null hides the reason for failure and leads to `NullReferenceException` bugs.

### When Exceptions Are Appropriate

Throw exceptions for:

- **Programmer errors** (invalid arguments, violated preconditions)
- **Unrecoverable failures** (database connection lost, configuration missing at startup)

Use Result for:

- **Expected failures** (API key not set, network timeout, validation errors)
- **Recoverable scenarios** where the caller decides what to do

---

## Method Design

### Keep Methods Small

- **Target: 10-20 lines** per method
- **Maximum: 30 lines** (soft guideline)
- If a method exceeds this, extract helper methods

### Keep Classes Small

- **Target: 100-150 lines** per class
- **Maximum: 200 lines** (soft guideline)
- **Single Responsibility**: Each class should have one reason to change

### Named Methods Over Complex Conditions

Extract complex logic into named methods that describe intent.

**Bad:**

```csharp
if (InProgressTasks.Count > 0)
    _windowMonitor.Start();
else
{
    _windowMonitor.Stop();
    CurrentProcessName = string.Empty;
    CurrentWindowTitle = string.Empty;
    FocusScore = 0;
    FocusReason = string.Empty;
    OnPropertyChanged(nameof(IsFocusScoreVisible));
}
```

**Good:**

```csharp
if (HasActiveTask())
    StartMonitoring();
else
    StopMonitoringAndResetFocusState();

private bool HasActiveTask() => InProgressTasks.Count > 0;

private void StartMonitoring() => _windowMonitor.Start();

private void StopMonitoringAndResetFocusState()
{
    _windowMonitor.Stop();
    ResetFocusState();
}

private void ResetFocusState()
{
    CurrentProcessName = string.Empty;
    CurrentWindowTitle = string.Empty;
    FocusScore = 0;
    FocusReason = string.Empty;
    OnPropertyChanged(nameof(IsFocusScoreVisible));
}
```

**Why:** Method names like `StopMonitoringAndResetFocusState` explain intent. Raw else blocks don't.

### Guard Clauses

Use early returns to reduce nesting:

**Bad:**

```csharp
public async Task ProcessAsync(string id)
{
    if (!string.IsNullOrEmpty(id))
    {
        var item = await _repo.GetByIdAsync(id);
        if (item != null)
        {
            // 20 lines of processing
        }
    }
}
```

**Good:**

```csharp
public async Task ProcessAsync(string id)
{
    if (string.IsNullOrEmpty(id))
        return;

    var item = await _repo.GetByIdAsync(id);
    if (item == null)
        return;

    // 20 lines of processing (not nested)
}
```

---

## Code Readability

### Step-by-Step Assignments

Prefer sequential, readable steps over nested function calls.

**Bad:**

```csharp
var result = someService.Process(
    transform(
        validate(
            parse(input, config), rules),
        options));
```

**Good:**

```csharp
var parsed = Parse(input, config);
var validated = Validate(parsed, rules);
var transformed = Transform(validated, options);
var result = someService.Process(transformed);
```

**Why:** Each line is a step you can read top-to-bottom. Easier to debug (set breakpoints on each step).

### Fluent Style (When Appropriate)

Fluent APIs are acceptable when they read naturally:

```csharp
var tasks = await context.UserTasks
    .Where(t => t.Status == TaskStatus.ToDo)
    .OrderByDescending(t => t.CreatedAt)
    .ToListAsync();
```

### Meaningful Variable Names

Use descriptive names, not abbreviations:

```csharp
// Bad
var t = await _repo.GetByIdAsync(id);
var res = await _svc.ClassifyAsync(desc, proc, win);

// Good
var task = await _repo.GetByIdAsync(taskId);
var alignmentResult = await _openAIService.ClassifyAlignmentAsync(taskDescription, processName, windowTitle);
```

---

## ViewModels

### CommunityToolkit.Mvvm

Use CommunityToolkit.Mvvm source generators:

```csharp
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _isSaving;

    public bool CanSave => !IsSaving && !string.IsNullOrWhiteSpace(ApiKey);

    [RelayCommand]
    private async Task SaveAsync()
    {
        // ...
    }
}
```

### Command Attributes

- Use `[RelayCommand]` for commands
- Use `AllowConcurrentExecutions = false` to prevent double-clicks:

```csharp
[RelayCommand(AllowConcurrentExecutions = false)]
private async Task AddTaskAsync()
{
    // Prevents multiple concurrent executions
}
```

### Property Change Notifications

Use `[NotifyPropertyChangedFor]` to chain property notifications:

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(ShowMaskedDisplay))]
[NotifyPropertyChangedFor(nameof(ShowInputArea))]
private bool _isEditing;
```

---

## Async Patterns

### Naming

Suffix async methods with `Async`:

```csharp
public async Task<UserTask> AddTaskAsync(string description)
public async Task LoadBoardAsync()
```

### CancellationToken

Accept `CancellationToken` for operations that may be cancelled:

```csharp
public async Task<Result<AlignmentResult>> ClassifyAlignmentAsync(
    string taskDescription,
    string processName,
    string windowTitle,
    CancellationToken ct = default)
{
    var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
    // ...
}
```

### Fire-and-Forget

For fire-and-forget async calls in constructors or event handlers, use discard:

```csharp
public KanbanBoardViewModel(...)
{
    _ = LoadBoardAsync();  // Fire-and-forget initialization
}
```

Consider logging exceptions in fire-and-forget scenarios:

```csharp
_ = LoadBoardAsync().ContinueWith(t =>
{
    if (t.IsFaulted)
        _logger.LogError(t.Exception, "Failed to load board");
}, TaskScheduler.Default);
```

---

## Documentation

### XML Documentation

Add XML docs to:

- **All public classes and interfaces**
- **Public methods with non-obvious behavior**

```csharp
/// <summary>
/// Service for classifying how aligned the current window is with the user's task.
/// </summary>
public interface IOpenAIService
{
    /// <summary>
    /// Classifies alignment of the given window/process with the task description.
    /// </summary>
    /// <param name="taskDescription">The user's current task.</param>
    /// <param name="processName">The foreground process name.</param>
    /// <param name="windowTitle">The foreground window title.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing alignment score and reason, or failure message.</returns>
    Task<Result<AlignmentResult>> ClassifyAlignmentAsync(
        string taskDescription,
        string processName,
        string windowTitle,
        CancellationToken ct = default);
}
```

### Comments

- **No emojis** in code comments
- Write comments only when the code cannot explain itself
- Prefer self-documenting code (good names, small methods) over comments

**Bad:**

```csharp
// Loop through tasks and add to list
foreach (var t in tasks)
    list.Add(t);
```

**Good (no comment needed):**

```csharp
foreach (var task in await _repo.GetToDoTasksAsync())
    ToDoTasks.Add(task);
```

---

## Summary Checklist

Before committing code, verify:

- [ ] No tuple returns (use named types)
- [ ] No null returns for expected failures (use Result)
- [ ] Methods are under 30 lines
- [ ] Classes are under 200 lines
- [ ] Complex conditions are extracted to named methods
- [ ] Variable names are descriptive
- [ ] Async methods are suffixed with `Async`
- [ ] Public APIs have XML documentation
- [ ] No emojis in comments
