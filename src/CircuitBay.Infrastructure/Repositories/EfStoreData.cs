using CircuitBay.Domain.Auditing;
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
using CircuitBay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CircuitBay.Infrastructure.Repositories;

public sealed class EfStoreData(CircuitBayDbContext db) : IStoreData
{
    public Task<ShopperAccount?> FindUserByEmailAsync(string email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(x => x.Email == email, ct);

    public Task<ShopperAccount?> FindUserAsync(Guid userId, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);

    public async Task AddUserAsync(ShopperAccount user, CancellationToken ct) => await db.Users.AddAsync(user, ct);

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct) =>
        await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task RevokeAllUserRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .ToArrayAsync(ct);
        foreach (var t in tokens)
            t.Revoke(now);
    }

    public async Task PurgeExpiredRefreshTokensAsync(DateTimeOffset expiredBefore, CancellationToken ct)
    {
        // Physical delete — these are not soft-deletable. Keep the last 90 days for audit trail.
        var old = await db.RefreshTokens
            .Where(x => x.ExpiresAt < expiredBefore)
            .ToArrayAsync(ct);
        db.RefreshTokens.RemoveRange(old);
    }

    public async Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken ct) =>
        await db.PasswordResetTokens.AddAsync(token, ct);

    public Task<PasswordResetToken?> FindPasswordResetTokenByHashAsync(string tokenHash, CancellationToken ct) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task InvalidateUserPasswordResetTokensAsync(Guid userId, CancellationToken ct)
    {
        // Mark any unused tokens as used so they can't be replayed
        var active = await db.PasswordResetTokens
            .Where(x => x.UserId == userId && !x.IsUsed)
            .ToArrayAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var t in active)
            t.MarkUsed(now);
    }

    public async Task<IReadOnlyCollection<Category>> ListCategoriesAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct);

    public Task<Category?> FindCategoryAsync(Guid id, CancellationToken ct) => db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddCategoryAsync(Category category, CancellationToken ct) => await db.Categories.AddAsync(category, ct);

    public Task<Product?> FindProductAsync(Guid id, CancellationToken ct) =>
        db.Products.Include(x => x.Specifications).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Product?> FindProductBySkuAsync(string sku, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(x => x.Sku == sku.Trim().ToUpper(), ct);

    public async Task AddProductAsync(Product product, CancellationToken ct) => await db.Products.AddAsync(product, ct);

    public async Task<PagedResult<ProductSummaryDto>> SearchProductsAsync(ProductSearchRequest request, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking().Include(x => x.Category).Where(x => x.IsPublished);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var q = request.Query.Trim();
            query = query.Where(x => x.Name.Contains(q) || x.Brand.Contains(q) || x.Sku.Contains(q));
        }
        if (request.CategoryId is not null) query = query.Where(x => x.CategoryId == request.CategoryId);
        if (!string.IsNullOrWhiteSpace(request.Brand)) query = query.Where(x => x.Brand == request.Brand);
        if (request.MinPrice is not null) query = query.Where(x => x.Price >= request.MinPrice);
        if (request.MaxPrice is not null) query = query.Where(x => x.Price <= request.MaxPrice);
        if (request.InStockOnly)
        {
            var stocked = db.Inventory.Where(i => i.Available - i.Reserved > 0).Select(i => i.ProductId);
            query = query.Where(x => stocked.Contains(x.Id));
        }

        query = request.Sort.ToLowerInvariant() switch
        {
            "price" => query.OrderBy(x => x.Price),
            "price_desc" => query.OrderByDescending(x => x.Price),
            "rating" => query.OrderByDescending(x => x.AverageRating).ThenBy(x => x.Name),
            "newest" => query.OrderByDescending(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.ReviewCount).ThenBy(x => x.Name)
        };

        var total = await query.CountAsync(ct);
        var page = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new ProductSummaryDto(
                x.Id,
                x.Sku,
                x.Name,
                x.Brand,
                x.Slug,
                x.Category == null ? "Uncategorized" : x.Category.Name,
                x.Price,
                x.Currency,
                db.Inventory.Where(i => i.ProductId == x.Id).Sum(i => i.Available > i.Reserved ? i.Available - i.Reserved : 0),
                x.AverageRating,
                x.ReviewCount))
            .ToArrayAsync(ct);

        return new PagedResult<ProductSummaryDto>(page, request.Page, request.PageSize, total);
    }

    public async Task<ProductDetailDto?> GetProductDetailAsync(Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Specifications)
            .Where(x => x.Id == id)
            .Select(x => new ProductDetailDto(
                x.Id,
                x.Sku,
                x.Name,
                x.Brand,
                x.Slug,
                x.Description,
                x.Category == null ? "Uncategorized" : x.Category.Name,
                x.Price,
                x.Currency,
                db.Inventory.Where(i => i.ProductId == x.Id).Sum(i => i.Available > i.Reserved ? i.Available - i.Reserved : 0),
                x.AverageRating,
                x.ReviewCount,
                x.Specifications.ToDictionary(s => s.Name, s => s.Value)))
            .FirstOrDefaultAsync(ct);

    public Task<InventoryItem?> FindInventoryAsync(Guid productId, string warehouseCode, CancellationToken ct) =>
        db.Inventory.FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseCode == warehouseCode.Trim().ToUpper(), ct);

    public async Task<IReadOnlyCollection<InventoryItem>> FindInventoryByProductAsync(Guid productId, CancellationToken ct) =>
        await db.Inventory.Where(x => x.ProductId == productId).ToArrayAsync(ct);

    public async Task<IReadOnlyCollection<InventoryItem>> ListInventoryAsync(CancellationToken ct) =>
        await db.Inventory.AsNoTracking().OrderBy(x => x.WarehouseCode).ToArrayAsync(ct);

    public async Task AddInventoryAsync(InventoryItem item, CancellationToken ct) => await db.Inventory.AddAsync(item, ct);

    public Task<Cart?> FindCartAsync(Guid shopperId, CancellationToken ct) =>
        db.Carts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.ShopperId == shopperId, ct);

    public async Task AddCartAsync(Cart cart, CancellationToken ct) => await db.Carts.AddAsync(cart, ct);

    public Task<Order?> FindOrderAsync(Guid orderId, CancellationToken ct) =>
        db.Orders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == orderId, ct);

    public async Task<IReadOnlyCollection<Order>> ListOrdersForUserAsync(Guid shopperId, CancellationToken ct) =>
        await db.Orders.Include(x => x.Lines).Where(x => x.ShopperId == shopperId).OrderByDescending(x => x.CreatedAt).ToArrayAsync(ct);

    public async Task AddOrderAsync(Order order, CancellationToken ct) => await db.Orders.AddAsync(order, ct);

    public async Task<int> CleanupAbandonedCartsAsync(DateTimeOffset staleBefore, CancellationToken ct)
    {
        var carts = await db.Carts.Include(x => x.Lines).Where(x => x.LastActivityAt < staleBefore && x.Lines.Any()).ToArrayAsync(ct);
        foreach (var cart in carts)
            cart.Clear();
        return carts.Length;
    }

    public async Task<int> MaintainPaidOrdersAsync(DateTimeOffset paidBefore, CancellationToken ct)
    {
        var orders = await db.Orders.Where(x => x.State == OrderState.Paid && x.CreatedAt < paidBefore).ToArrayAsync(ct);
        foreach (var order in orders)
            order.StartPacking();
        return orders.Length;
    }

    public Task<bool> HasCompletedOrderLineAsync(Guid shopperId, Guid productId, CancellationToken ct) =>
        db.Orders.AnyAsync(o => o.ShopperId == shopperId && o.State != OrderState.Cancelled && o.Lines.Any(l => l.ProductId == productId), ct);

    public Task<ProductReview?> FindReviewAsync(Guid shopperId, Guid productId, CancellationToken ct) =>
        db.ProductReviews.FirstOrDefaultAsync(x => x.ShopperId == shopperId && x.ProductId == productId, ct);

    public async Task AddReviewAsync(ProductReview review, CancellationToken ct) => await db.ProductReviews.AddAsync(review, ct);

    public async Task<IReadOnlyCollection<ProductReview>> ListReviewsAsync(Guid productId, CancellationToken ct) =>
        await db.ProductReviews.Where(x => x.ProductId == productId && !x.IsHidden).ToArrayAsync(ct);

    public Task<bool> WishlistContainsAsync(Guid shopperId, Guid productId, CancellationToken ct) =>
        db.WishlistItems.AnyAsync(x => x.ShopperId == shopperId && x.ProductId == productId, ct);

    public async Task AddWishlistItemAsync(WishlistItem item, CancellationToken ct) => await db.WishlistItems.AddAsync(item, ct);

    public async Task RemoveWishlistItemAsync(Guid shopperId, Guid productId, CancellationToken ct)
    {
        var item = await db.WishlistItems.FirstOrDefaultAsync(x => x.ShopperId == shopperId && x.ProductId == productId, ct);
        if (item is not null) db.WishlistItems.Remove(item);
    }

    public async Task<IReadOnlyCollection<ProductTinyDto>> ListWishlistAsync(Guid shopperId, CancellationToken ct) =>
        await db.WishlistItems.AsNoTracking()
            .Where(x => x.ShopperId == shopperId)
            .Join(db.Products, w => w.ProductId, p => p.Id, (w, p) => new ProductTinyDto(p.Id, p.Sku, p.Name, p.Price, p.Currency))
            .ToArrayAsync(ct);

    public Task<Coupon?> FindCouponByCodeAsync(string code, CancellationToken ct) =>
        db.Coupons.FirstOrDefaultAsync(x => x.Code == code.Trim().ToUpper(), ct);

    public async Task AddCouponAsync(Coupon coupon, CancellationToken ct) => await db.Coupons.AddAsync(coupon, ct);

    public async Task<IReadOnlyCollection<Coupon>> ListCouponsAsync(CancellationToken ct) =>
        await db.Coupons.AsNoTracking().OrderBy(x => x.Code).ToArrayAsync(ct);

    public async Task<int> DeactivateExpiredCouponsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var coupons = await db.Coupons.Where(x => x.IsActive && x.ExpiresAt <= now).ToArrayAsync(ct);
        foreach (var coupon in coupons)
            coupon.Deactivate();
        return coupons.Length;
    }

    public async Task AddAuditAsync(Guid? actorId, string action, string entityName, Guid? entityId, string summary, CancellationToken ct) =>
        await db.AuditLogs.AddAsync(new AuditLog(actorId, action, entityName, entityId, summary), ct);

    public async Task<IReadOnlyCollection<AuditLogDto>> ListAuditLogsAsync(int take, CancellationToken ct) =>
        await db.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new AuditLogDto(x.Id, x.ActorId, x.Action, x.EntityName, x.EntityId, x.Summary, x.CreatedAt))
            .ToArrayAsync(ct);

    public async Task<DashboardSnapshotDto> DashboardAsync(CancellationToken ct)
    {
        var lowStock = await db.Inventory.CountAsync(x => x.Available - x.Reserved <= x.ReorderPoint, ct);
        var openOrders = await db.Orders.CountAsync(x => x.State == OrderState.Paid || x.State == OrderState.Packing, ct);
        var revenueSince = DateTimeOffset.UtcNow.AddDays(-30);
        var revenue = await db.Orders.Where(x => x.CreatedAt >= revenueSince && x.State != OrderState.Cancelled).SumAsync(x => x.Total, ct);
        var products = await db.Products.CountAsync(x => x.IsPublished, ct);
        var notes = new List<string>();
        if (lowStock > 0) notes.Add($"{lowStock} inventory rows are at or below reorder point.");
        if (openOrders > 10) notes.Add("Packing queue is above the comfort threshold.");
        if (notes.Count == 0) notes.Add("No critical operational notes.");
        return new DashboardSnapshotDto(products, lowStock, openOrders, revenue, notes);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new CircuitBay.Application.Common.ConcurrencyAppException(
                "A concurrency conflict occurred. The data has been modified by another process.",
                ex);
        }
    }

    public void ClearTracker() => db.ChangeTracker.Clear();
}
