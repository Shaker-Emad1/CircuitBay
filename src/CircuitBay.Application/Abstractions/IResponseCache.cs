namespace CircuitBay.Application.Abstractions;

public interface IResponseCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
    Task<string> VersionAsync(string scope, CancellationToken ct);
    Task BumpVersionAsync(string scope, CancellationToken ct);
}
