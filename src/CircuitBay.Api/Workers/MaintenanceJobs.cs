using CircuitBay.Application.Services;

namespace CircuitBay.Api.Workers;

public sealed class MaintenanceJobs(MaintenanceService maintenance, ILogger<MaintenanceJobs> logger)
{
    public async Task CleanupAbandonedCartsAsync()
    {
        var count = await maintenance.CleanupAbandonedCartsAsync(CancellationToken.None);
        logger.LogInformation("Hangfire cleanup cleared {CartCount} abandoned carts.", count);
    }

    public async Task DeactivateExpiredCouponsAsync()
    {
        var count = await maintenance.DeactivateExpiredCouponsAsync(CancellationToken.None);
        logger.LogInformation("Hangfire deactivated {CouponCount} expired coupons.", count);
    }

    public async Task MaintainOrderStatesAsync()
    {
        var count = await maintenance.MaintainOrderStatesAsync(CancellationToken.None);
        logger.LogInformation("Hangfire advanced {OrderCount} paid orders into packing.", count);
    }

    public async Task PurgeExpiredRefreshTokensAsync()
    {
        await maintenance.PurgeExpiredRefreshTokensAsync(CancellationToken.None);
        logger.LogInformation("Hangfire purged expired refresh tokens.");
    }
}
