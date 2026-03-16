using System.Text;
using FocusBot.WebAPI.Data;
using FocusBot.WebAPI.Features.Auth;
using FocusBot.WebAPI.Features.Sessions;
using FocusBot.WebAPI.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Authentication ──────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var supabaseUrl = builder.Configuration["Supabase:Url"] ?? string.Empty;
        var jwtSecret = builder.Configuration["Supabase:JwtSecret"] ?? string.Empty;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

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
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware pipeline ─────────────────────────────────────────────────────
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("FocusBot API");
    });
}

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ───────────────────────────────────────────────────────────────
app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapSessionEndpoints();

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
