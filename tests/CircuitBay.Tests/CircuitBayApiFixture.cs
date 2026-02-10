using CircuitBay.Application.Abstractions;
using CircuitBay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace CircuitBay.Tests;

public sealed class CircuitBayApiFixture
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName =
        $"CircuitBayTests_{Guid.NewGuid():N}";

    private string _dbConnectionString = string.Empty;

    private readonly MsSqlContainer _dbContainer =
        new MsSqlBuilder()
            .WithPassword("Strong_test_password_2026!")
            .Build();

    private readonly RedisContainer _redisContainer =
        new RedisBuilder()
            .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await WaitForSqlAsync();

        _dbConnectionString =
            BuildDatabaseConnectionString();

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__CircuitBay",
            _dbConnectionString);

        await _dbContainer.ExecScriptAsync(
            $"CREATE DATABASE [{_databaseName}]");

        await _redisContainer.StartAsync();

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Redis",
            _redisContainer.GetConnectionString());

        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider
                .GetRequiredService<CircuitBayDbContext>();

        await db.Database.MigrateAsync();

        var hasher =
            scope.ServiceProvider
                .GetRequiredService<IPasswordHasher>();

        await SeedData.SeedAsync(db, hasher.Hash);
    }

    public new async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__CircuitBay",
            null);

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Redis",
            null);

        await Task.WhenAll(
            _dbContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (context, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:CircuitBay"] =
                            _dbConnectionString,

                        ["ConnectionStrings:Redis"] =
                            _redisContainer.GetConnectionString()
                    });
            });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<
                DbContextOptions<CircuitBayDbContext>>();

            services.AddDbContext<CircuitBayDbContext>(
                options => options.UseSqlServer(
                    _dbConnectionString,
                    sql => sql.EnableRetryOnFailure(
                        maxRetryCount: 6,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null)));
        });
    }

    private string BuildDatabaseConnectionString()
    {
        var builder =
            new SqlConnectionStringBuilder(
                _dbContainer.GetConnectionString())
            {
                InitialCatalog = _databaseName,
                TrustServerCertificate = true
            };

        builder["Encrypt"] = "False";

        return builder.ConnectionString;
    }

    private async Task WaitForSqlAsync()
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                var result =
                    await _dbContainer.ExecScriptAsync("SELECT 1");

                if (result.ExitCode == 0)
                    return;

                lastError =
                    new InvalidOperationException(
                        result.Stderr);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new InvalidOperationException(
            "The SQL Server test container did not become queryable.",
            lastError);
    }
}
