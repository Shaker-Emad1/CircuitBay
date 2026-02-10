using System.Text;
using System.Threading.RateLimiting;
using CircuitBay.Api.Middleware;
using CircuitBay.Api.Security;
using CircuitBay.Api.Workers;
using CircuitBay.Application.Abstractions;
using CircuitBay.Application.Services;
using CircuitBay.Infrastructure;
using CircuitBay.Infrastructure.Persistence;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));

var jwt = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtOptions>() ?? new JwtOptions();

var circuitBayConnectionString =
    builder.Configuration.GetConnectionString("CircuitBay")
    ?? throw new InvalidOperationException(
        "Connection string 'CircuitBay' is missing.");

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CommerceService>();
builder.Services.AddScoped<MaintenanceService>();
builder.Services.AddScoped<MaintenanceJobs>();

builder.Services.AddCircuitBayInfrastructure(
    builder.Configuration);

builder.Services.AddScoped<IJwtTokenFactory,
    JwtTokenFactory>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(
        CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        circuitBayConnectionString,
        new SqlServerStorageOptions
        {
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(15)
        }));

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfireServer();
}

builder.Services.AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,

                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwt.SigningKey)),

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", policy =>
    {
        policy.Window = TimeSpan.FromMinutes(15);
        policy.PermitLimit = 20;
        policy.QueueProcessingOrder =
            QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("api", policy =>
    {
        policy.Window = TimeSpan.FromMinutes(1);
        policy.PermitLimit = 120;
        policy.QueueProcessingOrder =
            QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0;
    });

    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;

        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                code = "rate_limited",
                message = "Too many requests. Slow down."
            },
            ct);
    };
});

builder.Services.AddHealthChecks()
    .AddSqlServer(
        circuitBayConnectionString,
        tags: ["ready"]);

var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddHealthChecks()
        .AddRedis(
            redisConnectionString,
            tags: ["ready"]);
}

var app = builder.Build();

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "CircuitBay API v1");

    options.RoutePrefix = "swagger";
});

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHangfireDashboard(
        "/ops/jobs",
        new DashboardOptions
        {
            Authorization =
            [
                new HangfireAdminAuthorizationFilter()
            ]
        });
}

app.MapControllers();

app.MapHealthChecks("/healthz");

app.MapHealthChecks(
    "/healthz/ready",
    new HealthCheckOptions
    {
        Predicate = check =>
            check.Tags.Contains("ready")
    });

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();

    var startupLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("CircuitBay.Startup");

    await WaitForSqlServerAsync(
        circuitBayConnectionString,
        app.Configuration,
        startupLogger);

    var db = scope.ServiceProvider
        .GetRequiredService<CircuitBayDbContext>();

    startupLogger.LogInformation(
        "Applying EF Core migrations for {DatabaseTarget}.",
        DescribeSqlTarget(circuitBayConnectionString));

    await db.Database.MigrateAsync();

    var hasher = scope.ServiceProvider
        .GetRequiredService<IPasswordHasher>();

    await SeedData.SeedAsync(db, hasher.Hash);

    startupLogger.LogInformation(
        "Database startup completed for {DatabaseTarget}.",
        DescribeSqlTarget(circuitBayConnectionString));
}

static async Task WaitForSqlServerAsync(
    string connectionString,
    IConfiguration configuration,
    ILogger logger)
{
    var attempts = Math.Max(
        1,
        configuration.GetValue(
            "Infrastructure:Startup:DatabaseMaxAttempts",
            20));

    var delaySeconds = Math.Max(
        1,
        configuration.GetValue(
            "Infrastructure:Startup:DatabaseDelaySeconds",
            3));

    var builder = new SqlConnectionStringBuilder(connectionString)
    {
        InitialCatalog = "master",
        ConnectTimeout = Math.Min(
            10,
            Math.Max(3, delaySeconds + 2))
    };

    var target = DescribeSqlTarget(connectionString);

    Exception? lastError = null;

    for (var attempt = 1; attempt <= attempts; attempt++)
    {
        try
        {
            await using var connection =
                new SqlConnection(builder.ConnectionString);

            await connection.OpenAsync();

            logger.LogInformation(
                "SQL Server is reachable at {DatabaseTarget} after {Attempt} attempt(s).",
                target,
                attempt);

            return;
        }
        catch (Exception ex)
        {
            lastError = ex;

            if (attempt == attempts)
                break;

            logger.LogWarning(
                ex,
                "SQL Server is not ready at {DatabaseTarget}. Attempt {Attempt}/{Attempts}; retrying in {DelaySeconds}s.",
                target,
                attempt,
                attempts,
                delaySeconds);

            await Task.Delay(
                TimeSpan.FromSeconds(delaySeconds));
        }
    }

    throw new InvalidOperationException(
        $"SQL Server did not become reachable at {target} after {attempts} attempts.",
        lastError);
}

static string DescribeSqlTarget(string connectionString)
{
    var builder = new SqlConnectionStringBuilder(connectionString);
    return $"{builder.DataSource}/{builder.InitialCatalog}";
}

if (!app.Environment.IsEnvironment("Testing"))
{
    RecurringJob.AddOrUpdate<MaintenanceJobs>(
        "abandoned-cart-cleanup",
        x => x.CleanupAbandonedCartsAsync(),
        Cron.Hourly);

    RecurringJob.AddOrUpdate<MaintenanceJobs>(
        "expired-coupon-deactivation",
        x => x.DeactivateExpiredCouponsAsync(),
        Cron.Daily);

    RecurringJob.AddOrUpdate<MaintenanceJobs>(
        "order-status-maintenance",
        x => x.MaintainOrderStatesAsync(),
        "*/30 * * * *");

    RecurringJob.AddOrUpdate<MaintenanceJobs>(
        "refresh-token-purge",
        x => x.PurgeExpiredRefreshTokensAsync(),
        Cron.Daily);
}

app.Run();

public partial class Program { }
