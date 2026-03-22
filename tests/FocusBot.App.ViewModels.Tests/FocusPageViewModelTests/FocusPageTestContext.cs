namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

/// <summary>
/// Placeholder for tests that previously shared an in-memory DB; kept for minimal churn in test methods.
/// </summary>
public sealed class FocusPageTestContext : IAsyncDisposable
{
    public static Task<FocusPageTestContext> CreateAsync() => Task.FromResult(new FocusPageTestContext());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
