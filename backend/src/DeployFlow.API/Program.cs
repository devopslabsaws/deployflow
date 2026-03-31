using DeployFlow.API;
using DeployFlow.API.Middleware;
using DeployFlow.Application;
using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure;
using DeployFlow.Infrastructure.Persistence;
using DeployFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ─── Application & Infrastructure Layers ──────────────────────────────────────
builder.Services.AddApplicationLayer();
builder.Services.AddInfrastructureLayer(builder.Configuration);
// NOTE: Identity (UserManager, RoleManager, stores, password options) is fully
// registered inside AddInfrastructureLayer — do NOT call AddIdentityCore again.

// Don't let a crashing background service take down the whole host.
// Each service already catches OperationCanceledException internally.
builder.Services.Configure<HostOptions>(opts =>
    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

// ─── HttpContext ──────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ─── CurrentUser (application-layer interface) ────────────────────────────────
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// ─── JWT Auth ─────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };

        // Support token via query string for SignalR hubs and SSE endpoints (EventSource cannot send headers)
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) &&
                    (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/api/logs/stream")))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ─── CORS ─────────────────────────────────────────────────────────────────────
// In production, set AllowedOrigins in appsettings / environment variables.
// In development, the frontend is proxied through Next.js so direct browser→API
// calls are rare; but we allow all localhost ports so any `next dev --port N`
// works without CORS failures (which surface as "Network Error" in the browser).
var configuredOrigins = builder.Configuration["AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(opts =>
    opts.AddPolicy("AllowFrontend", policy =>
    {
        if (configuredOrigins?.Length > 0)
        {
            policy.WithOrigins(configuredOrigins);
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Allow any localhost port during local development.
            policy.SetIsOriginAllowed(origin =>
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return uri.Host is "localhost" or "127.0.0.1";
                return false;
            });
        }
        else
        {
            policy.WithOrigins("http://localhost:3000");
        }
        policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    }));

// ─── Rate Limiting ────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 5;
    });
    opts.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ─── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ─── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddSingleton<DeployFlow.Application.Common.IDeploymentLogBroadcaster,
    DeployFlow.API.Services.SignalRDeploymentLogBroadcaster>();

// ─── Swagger ──────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DeployFlow API",
        Version = "v1",
        Description = "Enterprise-grade self-hosted deployment platform API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// ─── Build ────────────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ─── Migrate DB ───────────────────────────────────────────────────────────────
var runMigrationsOnStartup = builder.Configuration.GetValue("RunMigrationsOnStartup", true);
if (runMigrationsOnStartup)
{
    try
    {
        await app.Services.MigrateAndSeedAsync();
    }
    catch (Exception ex) when (app.Environment.IsDevelopment())
    {
        app.Logger.LogError(ex, "Database migration on startup failed in Development. Continuing startup with existing schema.");
    }
}

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeployFlow API v1"));
}

app.UseSerilogRequestLogging();
app.UseExceptionMiddleware();
app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<DeployFlow.API.Hubs.LogStreamHub>("/hubs/logs");
app.MapHub<DeployFlow.API.Hubs.DeploymentHub>("/hubs/deployments");

app.Run();
