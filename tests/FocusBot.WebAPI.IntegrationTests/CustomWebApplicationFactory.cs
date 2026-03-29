using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Features.Pricing;
using FocusBot.WebAPI.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FocusBot.WebAPI.IntegrationTests;

/// <summary>
/// Replaces PostgreSQL with in-memory database and provides test ES256 JWT config for integration tests.
/// Overrides the JWKS-based auth from Program.cs with a static test signing key.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestSupabaseUrl = "https://test-project.supabase.co";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = TestSupabaseUrl,
                ["ConnectionStrings:DefaultConnection"] = ""
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove JwksRefreshService so tests don't call external JWKS endpoints.
            var jwksDescriptors = services
                .Where(d => d.ServiceType == typeof(JwksRefreshService)
                            || d.ImplementationType == typeof(JwksRefreshService)
                            || d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                               && d.ImplementationFactory is not null)
                .ToList();

            foreach (var d in jwksDescriptors)
                services.Remove(d);

            // Override JWT Bearer to use the static test ES256 public key.
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = $"{TestSupabaseUrl}/auth/v1",
                        ValidateAudience = true,
                        ValidAudience = "authenticated",
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                        IssuerSigningKey = TestJwtHelper.PublicSecurityKey
                    };
                });

            // Replace PostgreSQL with InMemory database.
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

            foreach (var d in services
                         .Where(d =>
                             d.ServiceType == typeof(IPaddleBillingApi)
                             || d.ImplementationType == typeof(PaddleBillingApiClient))
                         .ToList())
                services.Remove(d);

            services.AddSingleton<IPaddleBillingApi, TestPaddleBillingApi>();
        });
    }
}
