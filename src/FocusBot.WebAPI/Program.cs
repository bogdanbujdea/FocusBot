using System.Threading.RateLimiting;
using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Features.Analytics;
using FocusBot.WebAPI.Features.Auth;
using FocusBot.WebAPI.Features.Classification;
using FocusBot.WebAPI.Features.Devices;
using FocusBot.WebAPI.Features.Sessions;
using FocusBot.WebAPI.Features.Subscriptions;
using FocusBot.WebAPI.Features.Waitlist;
using FocusBot.WebAPI.Hubs;
using FocusBot.WebAPI.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// ── Authentication (ES256 via Supabase JWKS) ────────────────────────────────
var jwksService = new JwksRefreshService(
    builder.Configuration,
    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<JwksRefreshService>()
);
builder.Services.AddSingleton(jwksService);
builder.Services.AddHostedService(sp => sp.GetRequiredService<JwksRefreshService>());

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var supabaseUrl = builder.Configuration["Supabase:Url"] ?? string.Empty;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            IssuerSigningKeyResolver = jwksService.ResolveSigningKeys,
        };
    });

builder.Services.AddAuthorization();

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── CORS ────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            var allowedOrigins =
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? Array.Empty<string>();

            if (allowedOrigins.Length > 0)
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowCredentials()
                    .WithHeaders(HeaderNames.ContentType, HeaderNames.Authorization)
                    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS");
            }
            else
            {
                policy.SetIsOriginAllowed(_ => true).AllowCredentials().AllowAnyHeader().AllowAnyMethod();
            }
        }
    );
});

// ── Rate limiting ───────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        "Waitlist",
        httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                ip,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
            );
        }
    );
});

// ── OpenAPI ─────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "Foqus API",
                Version = "v1",
                Description =
                    "Foqus backend API for focus sessions, classification, and subscriptions",
            };
            return Task.CompletedTask;
        }
    );

    options.AddDocumentTransformer(
        (document, _, _) =>
        {
            var components = document.Components ?? new OpenApiComponents();
            components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Supabase JWT access token",
            };
            document.Components = components;
            return Task.CompletedTask;
        }
    );
});

// ── Services ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<ClassificationService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<WaitlistService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.AddHttpClient(
    WaitlistService.HttpClientName,
    (sp, client) =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var apiKey = configuration["MailerLite:ApiKey"];

        client.BaseAddress = new Uri("https://connect.mailerlite.com/api/");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
    }
);

var app = builder.Build();

// ── Middleware pipeline ─────────────────────────────────────────────────────
app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Foqus API");
});

app.UseStaticFiles();

app.UseCors("Frontend");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ───────────────────────────────────────────────────────────────
app.MapHub<FocusHub>("/hubs/focus");
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok("Foqus API"));
app.MapAuthEndpoints();
app.MapSessionEndpoints();
app.MapClassificationEndpoints();
app.MapSubscriptionEndpoints();
app.MapWaitlistEndpoints();
app.MapDevicesEndpoints();
app.MapAnalyticsEndpoints();

// ── Database migration ──────────────────────────────────────────────────────
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program;
