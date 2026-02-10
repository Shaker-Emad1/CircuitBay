using System.Text.Json;
using CircuitBay.Application.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CircuitBay.Infrastructure.Caching;

public sealed class RedisResponseCache(IDistributedCache cache, ILogger<RedisResponseCache> logger) : IResponseCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        try
        {
            var payload = await cache.GetStringAsync(key, ct);
            return string.IsNullOrWhiteSpace(payload) ? default : JsonSerializer.Deserialize<T>(payload, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(value, JsonOptions), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for {CacheKey}", key);
        }
    }

    public Task RemoveAsync(string key, CancellationToken ct) => cache.RemoveAsync(key, ct);

    public async Task<string> VersionAsync(string scope, CancellationToken ct)
    {
        var key = $"version:{scope}";
        var version = await cache.GetStringAsync(key, ct);
        if (!string.IsNullOrWhiteSpace(version)) return version;

        version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        await cache.SetStringAsync(key, version, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
        }, ct);
        return version;
    }

    public async Task BumpVersionAsync(string scope, CancellationToken ct)
    {
        await cache.SetStringAsync($"version:{scope}", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
        }, ct);
    }
}
