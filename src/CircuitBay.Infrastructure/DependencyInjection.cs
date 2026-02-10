using CircuitBay.Application.Abstractions;
using CircuitBay.Infrastructure.Caching;
using CircuitBay.Infrastructure.Persistence;
using CircuitBay.Infrastructure.Repositories;
using CircuitBay.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CircuitBay.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCircuitBayInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CircuitBay")
            ?? throw new InvalidOperationException("Connection string 'CircuitBay' is missing.");

        services.AddDbContext<CircuitBayDbContext>(options => options.UseSqlServer(
            connectionString,
            sql => sql.EnableRetryOnFailure(
                maxRetryCount: 6,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null)));
        services.AddScoped<IStoreData, EfStoreData>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        var redis = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redis))
            services.AddDistributedMemoryCache();
        else
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
        services.AddSingleton<IResponseCache, RedisResponseCache>();
        return services;
    }
}
