using CircuitBay.Contracts.Catalog;
using CircuitBay.Contracts.Common;
using CircuitBay.Contracts.Operations;
using CircuitBay.Domain.Catalog;
using CircuitBay.Domain.Inventory;
using CircuitBay.Domain.Promotions;
using CircuitBay.Domain.Shopping;
using CircuitBay.Domain.Users;

namespace CircuitBay.Application.Abstractions;

public interface IStoreData
{
    Task<ShopperAccount?> FindUserByEmailAsync(string email, CancellationToken ct);
    Task<ShopperAccount?> FindUserAsync(Guid userId, CancellationToken ct);
    Task AddUserAsync(ShopperAccount user, CancellationToken ct);

    // Refresh tokens
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct);
    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken ct);
    Task RevokeAllUserRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct);
    Task PurgeExpiredRefreshTokensAsync(DateTimeOffset expiredBefore, CancellationToken ct);

    // Password reset
    Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken ct);
    Task<PasswordResetToken?> FindPasswordResetTokenByHashAsync(string tokenHash, CancellationToken ct);
    Task InvalidateUserPasswordResetTokensAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyCollection<Category>> ListCategoriesAsync(CancellationToken ct);
    Task<Category?> FindCategoryAsync(Guid id, CancellationToken ct);
    Task AddCategoryAsync(Category category, CancellationToken ct);

    Task<Product?> FindProductAsync(Guid id, CancellationToken ct);
    Task<Product?> FindProductBySkuAsync(string sku, CancellationToken ct);
    Task AddProductAsync(Product product, CancellationToken ct);
    Task<PagedResult<ProductSummaryDto>> SearchProductsAsync(ProductSearchRequest request, CancellationToken ct);
    Task<ProductDetailDto?> GetProductDetailAsync(Guid id, CancellationToken ct);

    Task<InventoryItem?> FindInventoryAsync(Guid productId, string warehouseCode, CancellationToken ct);
    Task<IReadOnlyCollection<InventoryItem>> FindInventoryByProductAsync(Guid productId, CancellationToken ct);
    Task<IReadOnlyCollection<InventoryItem>> ListInventoryAsync(CancellationToken ct);
    Task AddInventoryAsync(InventoryItem item, CancellationToken ct);

    Task<Cart?> FindCartAsync(Guid shopperId, CancellationToken ct);
    Task AddCartAsync(Cart cart, CancellationToken ct);
    Task<Order?> FindOrderAsync(Guid orderId, CancellationToken ct);
    Task<IReadOnlyCollection<Order>> ListOrdersForUserAsync(Guid shopperId, CancellationToken ct);
    Task AddOrderAsync(Order order, CancellationToken ct);
    Task<int> CleanupAbandonedCartsAsync(DateTimeOffset staleBefore, CancellationToken ct);
    Task<int> MaintainPaidOrdersAsync(DateTimeOffset paidBefore, CancellationToken ct);

    Task<bool> HasCompletedOrderLineAsync(Guid shopperId, Guid productId, CancellationToken ct);
    Task<ProductReview?> FindReviewAsync(Guid shopperId, Guid productId, CancellationToken ct);
    Task AddReviewAsync(ProductReview review, CancellationToken ct);
    Task<IReadOnlyCollection<ProductReview>> ListReviewsAsync(Guid productId, CancellationToken ct);

    Task<bool> WishlistContainsAsync(Guid shopperId, Guid productId, CancellationToken ct);
    Task AddWishlistItemAsync(WishlistItem item, CancellationToken ct);
    Task RemoveWishlistItemAsync(Guid shopperId, Guid productId, CancellationToken ct);
    Task<IReadOnlyCollection<ProductTinyDto>> ListWishlistAsync(Guid shopperId, CancellationToken ct);

    Task<Coupon?> FindCouponByCodeAsync(string code, CancellationToken ct);
    Task AddCouponAsync(Coupon coupon, CancellationToken ct);
    Task<IReadOnlyCollection<Coupon>> ListCouponsAsync(CancellationToken ct);
    Task<int> DeactivateExpiredCouponsAsync(DateTimeOffset now, CancellationToken ct);

    Task AddAuditAsync(Guid? actorId, string action, string entityName, Guid? entityId, string summary, CancellationToken ct);
    Task<IReadOnlyCollection<AuditLogDto>> ListAuditLogsAsync(int take, CancellationToken ct);

    Task<DashboardSnapshotDto> DashboardAsync(CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
    void ClearTracker();
}

