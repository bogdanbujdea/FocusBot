using FocusBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusBot.Infrastructure.Tests.Data;

public abstract class TaskRepositoryTestBase : IDisposable
{
    protected AppDbContext Context { get; }
    protected TaskRepository Repository { get; }

    protected TaskRepositoryTestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        Context = new AppDbContext(options);
        Repository = new TaskRepository(Context);
        Context.Database.EnsureCreated();
    }

    public void Dispose() => Context.Dispose();
}
