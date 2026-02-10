using CircuitBay.Application.Abstractions;
using CircuitBay.Contracts.Catalog;
using CircuitBay.Contracts.Common;
using CircuitBay.Contracts.Operations;
using CircuitBay.Domain.Catalog;
using CircuitBay.Domain.Enums;
using CircuitBay.Domain.Inventory;
using CircuitBay.Domain.Promotions;
using CircuitBay.Domain.Shopping;
using CircuitBay.Domain.Users;

namespace CircuitBay.Tests;

internal sealed class MemoryStore : IStoreData
{
    private readonly List<ShopperAccount> _users = [];
    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<PasswordResetToken> _passwordResetTokens = [];
    private readonly List<Category> _categories = [];
    private readonly List<Product> _products = [];
    private readonly List<InventoryItem> _inventory = [];
    private readonly List<Cart> _carts = [];
    private readonly List<Order> _orders = [];
    private readonly List<ProductReview> _reviews = [];
    private readonly List<WishlistItem> _wishlist = [];
    private readonly List<Coupon> _coupons = [];
    private readonly List<AuditLogDto> _audit = [];

    public int SaveCount { get; private set; }
    public IReadOnlyList<AuditLogDto> AuditLog => _audit;

    public Task<Category> CreateCategoryAsync(string name)
    {
        var category = new Category(name, name.ToLowerInvariant());
        _categories.Add(category);
        return Task.FromResult(category);
    }

    public Task<ShopperAccount?> FindUserByEmailAsync(string email, CancellationToken ct) => Task.FromResult(_users.FirstOrDefault(x => x.Email == email));
    public Task<ShopperAccount?> FindUserAsync(Guid userId, CancellationToken ct) => Task.FromResult(_users.FirstOrDefault(x => x.Id == userId));
    public Task AddUserAsync(ShopperAccount user, CancellationToken ct) { _users.Add(user); return Task.CompletedTask; }

    public Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct) { _refreshTokens.Add(token); return Task.CompletedTask; }
    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken ct) =>
        Task.FromResult(_refreshTokens.FirstOrDefault(x => x.TokenHash == tokenHash));
    public Task RevokeAllUserRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var t in _refreshTokens.Where(x => x.UserId == userId && !x.IsRevoked))
            t.Revoke(now);
        return Task.CompletedTask;
    }
    public Task PurgeExpiredRefreshTokensAsync(DateTimeOffset expiredBefore, CancellationToken ct)
    {
        _refreshTokens.RemoveAll(x => x.ExpiresAt < expiredBefore);
        return Task.CompletedTask;
    }

    public Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken ct) { _passwordResetTokens.Add(token); return Task.CompletedTask; }
    public Task<PasswordResetToken?> FindPasswordResetTokenByHashAsync(string tokenHash, CancellationToken ct) =>
        Task.FromResult(_passwordResetTokens.FirstOrDefault(x => x.TokenHash == tokenHash));
    public Task InvalidateUserPasswordResetTokensAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var t in _passwordResetTokens.Where(x => x.UserId == userId && !x.IsUsed))
            t.MarkUsed(now);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Category>> ListCategoriesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Category>>(_categories);
    public Task<Category?> FindCategoryAsync(Guid id, CancellationToken ct) => Task.FromResult(_categories.FirstOrDefault(x => x.Id == id));
    public Task AddCategoryAsync(Category category, CancellationToken ct) { _categories.Add(category); return Task.CompletedTask; }
    public Task<Product?> FindProductAsync(Guid id, CancellationToken ct) => Task.FromResult(_products.FirstOrDefault(x => x.Id == id));
    public Task<Product?> FindProductBySkuAsync(string sku, CancellationToken ct) => Task.FromResult(_products.FirstOrDefault(x => x.Sku == sku.Trim().ToUpperInvariant()));
    public Task AddProductAsync(Product product, CancellationToken ct) { _products.Add(product); return Task.CompletedTask; }
    public Task<PagedResult<ProductSummaryDto>> SearchProductsAsync(ProductSearchRequest request, CancellationToken ct) => Task.FromResult(new PagedResult<ProductSummaryDto>([], request.Page, request.PageSize, 0));
    public Task<ProductDetailDto?> GetProductDetailAsync(Guid id, CancellationToken ct) => Task.FromResult<ProductDetailDto?>(null);
    public Task<InventoryItem?> FindInventoryAsync(Guid productId, string warehouseCode, CancellationToken ct) => Task.FromResult(_inventory.FirstOrDefault(x => x.ProductId == productId && x.WarehouseCode == warehouseCode));
    public Task<IReadOnlyCollection<InventoryItem>> FindInventoryByProductAsync(Guid productId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<InventoryItem>>(_inventory.Where(x => x.ProductId == productId).ToArray());
    public Task<IReadOnlyCollection<InventoryItem>> ListInventoryAsync(CancellationToken ct) => Task.FromResult<IReadOnlyCollection<InventoryItem>>(_inventory);
    public Task AddInventoryAsync(InventoryItem item, CancellationToken ct) { _inventory.Add(item); return Task.CompletedTask; }
    public Task<Cart?> FindCartAsync(Guid shopperId, CancellationToken ct) => Task.FromResult(_carts.FirstOrDefault(x => x.ShopperId == shopperId));
    public Task AddCartAsync(Cart cart, CancellationToken ct) { _carts.Add(cart); return Task.CompletedTask; }
    public Task<Order?> FindOrderAsync(Guid orderId, CancellationToken ct) => Task.FromResult(_orders.FirstOrDefault(x => x.Id == orderId));
    public Task<IReadOnlyCollection<Order>> ListOrdersForUserAsync(Guid shopperId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Order>>(_orders.Where(x => x.ShopperId == shopperId).ToArray());
    public Task AddOrderAsync(Order order, CancellationToken ct) { _orders.Add(order); return Task.CompletedTask; }
    public Task<int> CleanupAbandonedCartsAsync(DateTimeOffset staleBefore, CancellationToken ct)
    {
        var carts = _carts.Where(x => x.LastActivityAt < staleBefore && x.Lines.Any()).ToArray();
        foreach (var cart in carts) cart.Clear();
        return Task.FromResult(carts.Length);
    }
    public Task<int> MaintainPaidOrdersAsync(DateTimeOffset paidBefore, CancellationToken ct)
    {
        var orders = _orders.Where(x => x.State == OrderState.Paid && x.CreatedAt < paidBefore).ToArray();
        foreach (var order in orders) order.StartPacking();
        return Task.FromResult(orders.Length);
    }
    public Task<bool> HasCompletedOrderLineAsync(Guid shopperId, Guid productId, CancellationToken ct) => Task.FromResult(_orders.Any(o => o.ShopperId == shopperId && o.State != OrderState.Cancelled && o.Lines.Any(l => l.ProductId == productId)));
    public Task<ProductReview?> FindReviewAsync(Guid shopperId, Guid productId, CancellationToken ct) => Task.FromResult(_reviews.FirstOrDefault(x => x.ShopperId == shopperId && x.ProductId == productId));
    public Task AddReviewAsync(ProductReview review, CancellationToken ct) { _reviews.Add(review); return Task.CompletedTask; }
    public Task<IReadOnlyCollection<ProductReview>> ListReviewsAsync(Guid productId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ProductReview>>(_reviews.Where(x => x.ProductId == productId).ToArray());
    public Task<bool> WishlistContainsAsync(Guid shopperId, Guid productId, CancellationToken ct) => Task.FromResult(_wishlist.Any(x => x.ShopperId == shopperId && x.ProductId == productId));
    public Task AddWishlistItemAsync(WishlistItem item, CancellationToken ct) { _wishlist.Add(item); return Task.CompletedTask; }
    public Task RemoveWishlistItemAsync(Guid shopperId, Guid productId, CancellationToken ct) { _wishlist.RemoveAll(x => x.ShopperId == shopperId && x.ProductId == productId); return Task.CompletedTask; }
    public Task<IReadOnlyCollection<ProductTinyDto>> ListWishlistAsync(Guid shopperId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ProductTinyDto>>([]);
    public Task<Coupon?> FindCouponByCodeAsync(string code, CancellationToken ct) => Task.FromResult(_coupons.FirstOrDefault(x => x.Code == code.Trim().ToUpperInvariant()));
    public Task AddCouponAsync(Coupon coupon, CancellationToken ct) { _coupons.Add(coupon); return Task.CompletedTask; }
    public Task<IReadOnlyCollection<Coupon>> ListCouponsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Coupon>>(_coupons);
    public Task<int> DeactivateExpiredCouponsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var coupons = _coupons.Where(x => x.IsActive && x.ExpiresAt <= now).ToArray();
        foreach (var coupon in coupons) coupon.Deactivate();
        return Task.FromResult(coupons.Length);
    }
    public Task AddAuditAsync(Guid? actorId, string action, string entityName, Guid? entityId, string summary, CancellationToken ct)
    {
        _audit.Add(new AuditLogDto(Guid.NewGuid(), actorId, action, entityName, entityId, summary, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }
    public Task<IReadOnlyCollection<AuditLogDto>> ListAuditLogsAsync(int take, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<AuditLogDto>>(_audit.Take(take).ToArray());
    public Task<DashboardSnapshotDto> DashboardAsync(CancellationToken ct) => Task.FromResult(new DashboardSnapshotDto(_products.Count, 0, 0, 0, []));
    public Task<int> SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.FromResult(1); }
    public void ClearTracker() { }
}

