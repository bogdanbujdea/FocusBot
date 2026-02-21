using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using FocusBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusBot.Infrastructure.Tests.Services;

public abstract class FocusScoreServiceTestBase : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    protected FocusScoreService Service { get; }
    protected AppDbContext Context { get; }

    protected FocusScoreServiceTestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddScoped(_ => Context);
        services.AddScoped<ITaskRepository, TaskRepository>();
        _serviceProvider = services.BuildServiceProvider();
        Service = new FocusScoreService(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
    }

    public void Dispose()
    {
        Context.Dispose();
        _serviceProvider.Dispose();
    }
}
