using CircuitBay.Application.Abstractions;

namespace CircuitBay.Application.Services;

public sealed class MaintenanceService(IStoreData data, IClock clock)
{
    public async Task<int> CleanupAbandonedCartsAsync(CancellationToken ct)
    {
        var removed = await data.CleanupAbandonedCartsAsync(clock.UtcNow.AddDays(-21), ct);
        await data.SaveChangesAsync(ct);
        return removed;
    }

    public async Task<int> DeactivateExpiredCouponsAsync(CancellationToken ct)
    {
        var changed = await data.DeactivateExpiredCouponsAsync(clock.UtcNow, ct);
        await data.SaveChangesAsync(ct);
        return changed;
    }

    public async Task<int> MaintainOrderStatesAsync(CancellationToken ct)
    {
        var changed = await data.MaintainPaidOrdersAsync(clock.UtcNow.AddHours(-2), ct);
        await data.SaveChangesAsync(ct);
        return changed;
    }

    public async Task PurgeExpiredRefreshTokensAsync(CancellationToken ct)
    {
        await data.PurgeExpiredRefreshTokensAsync(clock.UtcNow.AddDays(-90), ct);
        await data.SaveChangesAsync(ct);
    }
}
