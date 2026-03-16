using FocusBot.WebAPI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FocusBot.WebAPI.IntegrationTests;

/// <summary>
/// Replaces PostgreSQL with in-memory database and provides test JWT config for integration tests.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "this-is-a-test-jwt-secret-that-is-at-least-32-characters-long!!";
    public const string TestSupabaseUrl = "https://test-project.supabase.co";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = TestSupabaseUrl,
                ["Supabase:JwtSecret"] = TestJwtSecret,
                ["ConnectionStrings:DefaultConnection"] = ""
            });
        });

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApiDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)
                    || d.ServiceType == typeof(ApiDbContext)
                    || (d.ServiceType.IsGenericType &&
                        d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)))
                .ToList();

            foreach (var d in toRemove)
                services.Remove(d);

            var dbName = $"IntegrationTests_{Guid.NewGuid()}";

            services.AddScoped<ApiDbContext>(_ =>
            {
                var options = new DbContextOptionsBuilder<ApiDbContext>()
                    .UseInMemoryDatabase(dbName)
                    .Options;
                return new ApiDbContext(options);
            });
        });
    }
}
