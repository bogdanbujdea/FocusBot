using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Features.Auth;
using FocusBot.WebAPI.Features.Classification;
using FocusBot.WebAPI.Features.Sessions;
using FocusBot.WebAPI.Features.Subscriptions;
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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Authentication (ES256 via Supabase JWKS) ────────────────────────────────
var jwksService = new JwksRefreshService(
    builder.Configuration,
    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<JwksRefreshService>());
builder.Services.AddSingleton(jwksService);
builder.Services.AddHostedService(sp => sp.GetRequiredService<JwksRefreshService>());

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKeyResolver = jwksService.ResolveSigningKeys
        };
    });

builder.Services.AddAuthorization();

// ── CORS ────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .WithHeaders(HeaderNames.ContentType, HeaderNames.Authorization)
                  .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS");
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// ── OpenAPI ─────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "FocusBot API",
            Version = "v1",
            Description = "FocusBot backend API for focus sessions, classification, and subscriptions"
        };
        return Task.CompletedTask;
    });

    options.AddDocumentTransformer((document, _, _) =>
    {
        var components = document.Components ?? new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Supabase JWT access token"
        };
        document.Components = components;
        return Task.CompletedTask;
    });
});

// ── Services ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<ClassificationService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware pipeline ─────────────────────────────────────────────────────
app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("FocusBot API");
});

app.UseStaticFiles();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ───────────────────────────────────────────────────────────────
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok("FocusBot API"));
app.MapAuthEndpoints();
app.MapSessionEndpoints();
app.MapClassificationEndpoints();
app.MapSubscriptionEndpoints();

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
