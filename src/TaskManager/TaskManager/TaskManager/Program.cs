using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;
using TaskManager.Authorization;
using TaskManager.Components;
using TaskManager.Data;
using TaskManager.Data.Repositories;
using TaskManager.Middleware;
using TaskManager.Services;
using TaskManager.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "TaskManager")
    .WriteTo.Console());

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// FluentValidation: auto-register every AbstractValidator<T> in this assembly.
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<TaskManager.Validation.LoginDtoValidator>();

// Force every API validation error and unhandled API exception into a consistent
// application/problem+json payload (RFC 7807) so the client always knows the shape.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = false;
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kv => kv.Value!.Errors.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };

        return new BadRequestObjectResult(problem);
    };
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is required. Configure it with user-secrets or an environment variable.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDataProtection()
    .SetApplicationName("TaskManager")
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

// Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecret = jwtSettings["SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey must be at least 32 characters. Configure it with user-secrets or an environment variable.");
}

var secretKey = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ClockSkew = TimeSpan.Zero
    };

    // IMPORTANT: Don't challenge on unauthorized for non-API requests
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            // Only challenge API requests
            if (!context.Request.Path.StartsWithSegments("/api") &&
                !context.Request.Path.StartsWithSegments("/hubs"))
            {
                context.HandleResponse();
            }
            return Task.CompletedTask;
        }
    };
})
.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
    ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

// CORS
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Register services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ICollaborationService, CollaborationService>();

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<EmailJobs>();
builder.Services.AddScoped<Phase6Jobs>();
builder.Services.AddScoped<Phase7Jobs>();
builder.Services.AddScoped<IOutboundEventPublisher, OutboundEventPublisher>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddHttpClient("OutboundWebhooks", c => c.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

// Billing & subscription entitlements
builder.Services.AddMemoryCache();
builder.Services.Configure<TaskManager.Services.Billing.RazorpayOptions>(
    builder.Configuration.GetSection(TaskManager.Services.Billing.RazorpayOptions.SectionName));
builder.Services.AddHttpClient<TaskManager.Services.Billing.IBillingProvider,
    TaskManager.Services.Billing.RazorpayBillingProvider>();
builder.Services.AddScoped<TaskManager.Services.Billing.RazorpayPlanSyncService>();
builder.Services.AddScoped<TaskManager.Services.Billing.IEntitlementService,
    TaskManager.Services.Billing.EntitlementService>();

// Repository + UnitOfWork data-access layer
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Add cascading authentication state for server-side
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seedOptions = app.Configuration
            .GetSection(SeedOptions.SectionName)
            .Get<SeedOptions>() ?? new SeedOptions();
        var initializationLogger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitialization");

        await DbInitializer.InitializeAsync(context, seedOptions, initializationLogger);
    }
    catch (Exception exception)
    {
        app.Logger.LogError(
            exception,
            "Database initialization failed. The application will stay live but remain unready.");
    }
}

try
{
    using var billingScope = app.Services.CreateScope();
    var planSync = billingScope.ServiceProvider
        .GetRequiredService<TaskManager.Services.Billing.RazorpayPlanSyncService>();
    await planSync.SyncAsync();
}
catch (Exception exception)
{
    app.Logger.LogWarning(
        exception,
        "Razorpay plan sync failed. Checkout will stay unavailable until provider plans are linked.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    // In development, we do not force HTTPS redirection so that local mobile emulators 
    // can connect over cleartext HTTP (10.0.2.2) without encountering SSL handshake errors.
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("AllowBlazorClient");

// Global exception handler — wraps the whole pipeline, emits problem+json on failure.
app.UseMiddleware<TaskManager.Middleware.ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthFilter()],
    DashboardTitle = "TaskManager Jobs"
});

RecurringJob.AddOrUpdate<Phase6Jobs>(
    "phase6-recurring-tasks",
    j => j.ProcessRecurringTasksAsync(),
    Cron.Hourly);

RecurringJob.AddOrUpdate<Phase7Jobs>(
    "phase7-webhook-retries",
    j => j.RetryPendingWebhooksAsync(),
    Cron.Minutely);

app.UseAntiforgery();

app.MapStaticAssets();

// Map Blazor components - Allow anonymous
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(TaskManager.Client._Imports).Assembly)
    .AllowAnonymous(); // EXPLICITLY allow anonymous

// Map controllers with selective authorization
app.MapControllers();
//.RequireAuthorization();

app.MapHub<TaskManager.Hubs.TaskHub>(TaskManager.Hubs.TaskHub.HubPath);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();