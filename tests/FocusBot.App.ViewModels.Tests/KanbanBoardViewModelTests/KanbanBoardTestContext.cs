using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.App.ViewModels.Tests.KanbanBoardViewModelTests;

/// <summary>
/// Shared in-memory DB and repo for KanbanBoardViewModel tests. Disposes the DbContext when disposed.
/// </summary>
public sealed class KanbanBoardTestContext : IAsyncDisposable
{
    private readonly AppDbContext _context;

    public ITaskRepository Repo { get; }

    private KanbanBoardTestContext(AppDbContext context, ITaskRepository repo)
    {
        _context = context;
        Repo = repo;
    }

    public static async Task<KanbanBoardTestContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var repo = new TaskRepository(context);
        return new KanbanBoardTestContext(context, repo);
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
