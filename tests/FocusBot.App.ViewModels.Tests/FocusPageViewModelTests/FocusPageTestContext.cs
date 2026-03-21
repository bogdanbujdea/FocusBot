using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.App.ViewModels.Tests.FocusPageViewModelTests;

/// <summary>
/// Shared in-memory DB and repo for FocusPageViewModel tests. Disposes the DbContext when disposed.
/// </summary>
public sealed class FocusPageTestContext : IAsyncDisposable
{
    private readonly AppDbContext _context;

    public ISessionRepository Repo { get; }

    private FocusPageTestContext(AppDbContext context, ISessionRepository repo)
    {
        _context = context;
        Repo = repo;
    }

    public static async Task<FocusPageTestContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var repo = new SessionRepository(context);
        return new FocusPageTestContext(context, repo);
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
