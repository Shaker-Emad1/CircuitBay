using CircuitBay.Application.Abstractions;
using CircuitBay.Application.Common;
using CircuitBay.Contracts.Operations;
using CircuitBay.Domain.Inventory;
using CircuitBay.Domain.Promotions;
using CircuitBay.Domain.Shopping;

namespace CircuitBay.Application.Services;

public sealed class CommerceService(IStoreData data, IClock? clock = null)
{
    private DateTimeOffset UtcNow => (clock ?? new SystemClock()).UtcNow;

    public async Task<InventoryDto> ReceiveInventoryAsync(InventoryReceiptRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0) throw new ValidationAppException("Receipt quantity must be positive.");
        var product = await data.FindProductAsync(request.ProductId, ct) ?? throw new NotFoundAppException("Product was not found.");
        var inventory = await data.FindInventoryAsync(request.ProductId, request.WarehouseCode, ct);

        if (inventory is null)
        {
            inventory = new InventoryItem(request.ProductId, request.WarehouseCode, request.Quantity, request.ReorderPoint);
            await data.AddInventoryAsync(inventory, ct);
        }
        else
        {
            inventory.Receive(request.Quantity);
        }

        await data.SaveChangesAsync(ct);
        await data.AddAuditAsync(null, "inventory.received", nameof(InventoryItem), inventory.Id, $"{request.Quantity} units received for {product.Sku} at {inventory.WarehouseCode}.", ct);
        await data.SaveChangesAsync(ct);
        return new InventoryDto(product.Id, product.Sku, product.Name, inventory.WarehouseCode, inventory.Available, inventory.Reserved, inventory.Sellable, inventory.ReorderPoint);
    }

    public async Task<IReadOnlyCollection<InventoryDto>> InventoryAsync(CancellationToken ct)
    {
        var rows = new List<InventoryDto>();
        foreach (var item in await data.ListInventoryAsync(ct))
        {
            var product = await data.FindProductAsync(item.ProductId, ct);
            if (product is not null)
                rows.Add(new InventoryDto(product.Id, product.Sku, product.Name, item.WarehouseCode, item.Available, item.Reserved, item.Sellable, item.ReorderPoint));
        }
        return rows;
    }

    public async Task<CartDto> GetCartAsync(CurrentUser user, CancellationToken ct)
    {
        var cart = await EnsureCartAsync(user.Id, ct);
        return await MapCartAsync(cart, ct);
    }

    public async Task<CartDto> ApplyCouponAsync(CurrentUser user, ApplyCouponRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationAppException("Coupon code is required.");

        var coupon = await data.FindCouponByCodeAsync(request.Code, ct) ?? throw new NotFoundAppException("Coupon was not found.");
        var cart = await EnsureCartAsync(user.Id, ct);
        var subtotal = cart.Lines.Sum(x => x.Quantity * x.UnitPrice);
        decimal discount;
        try
        {
            discount = coupon.CalculateDiscount(subtotal, UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            throw new ValidationAppException(ex.Message);
        }

        cart.ApplyCoupon(coupon.Code, discount);
        await data.AddAuditAsync(user.Id, "coupon.applied", nameof(Coupon), coupon.Id, $"{coupon.Code} reserved against cart {cart.Id} for {discount:C}.", ct);
        await data.SaveChangesAsync(ct);
        return await MapCartAsync(cart, ct);
    }

    public async Task<CartDto> RemoveCouponAsync(CurrentUser user, CancellationToken ct)
    {
        var cart = await EnsureCartAsync(user.Id, ct);
        cart.ClearCoupon();
        await data.SaveChangesAsync(ct);
        return await MapCartAsync(cart, ct);
    }

    public async Task<CartDto> PutCartLineAsync(CurrentUser user, CartLineRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0 || request.Quantity > 9)
            throw new ValidationAppException("Cart quantity must be between 1 and 9.");

        var product = await data.FindProductAsync(request.ProductId, ct) ?? throw new NotFoundAppException("Product was not found.");
        var stock = (await data.FindInventoryByProductAsync(product.Id, ct)).Sum(x => x.Sellable);
        if (!product.IsPublished || stock < request.Quantity)
            throw new ConflictAppException("Requested quantity is not available.");

        var cart = await EnsureCartAsync(user.Id, ct);
        cart.AddOrUpdate(product.Id, request.Quantity, product.Price, product.Currency);
        await data.SaveChangesAsync(ct);
        return await MapCartAsync(cart, ct);
    }

    public async Task RemoveCartLineAsync(CurrentUser user, Guid productId, CancellationToken ct)
    {
        var cart = await EnsureCartAsync(user.Id, ct);
        cart.Remove(productId);
        await data.SaveChangesAsync(ct);
    }

    public async Task<OrderDto> CheckoutAsync(CurrentUser user, CheckoutRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ShippingAddress) || string.IsNullOrWhiteSpace(request.ShippingName))
            throw new ValidationAppException("Shipping name and address are required.");

        var account = await data.FindUserAsync(user.Id, ct) ?? throw new UnauthorizedAppException("User session is no longer valid.");

        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var cart = await EnsureCartAsync(user.Id, ct);
                if (!cart.Lines.Any()) throw new ValidationAppException("Cart is empty.");

                var order = new Order(user.Id, account.Email, request.ShippingName, request.ShippingAddress);
                foreach (var line in cart.Lines)
                {
                    var product = await data.FindProductAsync(line.ProductId, ct) ?? throw new NotFoundAppException("A cart product no longer exists.");
                    var inventories = (await data.FindInventoryByProductAsync(product.Id, ct)).OrderByDescending(x => x.Sellable).ToArray();
                    var remaining = line.Quantity;
                    foreach (var item in inventories)
                    {
                        var take = Math.Min(item.Sellable, remaining);
                        if (take <= 0) continue;
                        item.Reserve(take);
                        remaining -= take;
                        if (remaining == 0) break;
                    }
                    if (remaining > 0) throw new ConflictAppException($"{product.Name} went out of stock.");
                    order.AddLine(product.Id, product.Sku, product.Name, line.Quantity, line.UnitPrice, product.Currency);
                }

                if (!string.IsNullOrWhiteSpace(cart.AppliedCouponCode))
                {
                    var coupon = await data.FindCouponByCodeAsync(cart.AppliedCouponCode, ct) ?? throw new ValidationAppException("Applied coupon is no longer available.");
                    try
                    {
                        var discount = coupon.CalculateDiscount(order.Subtotal, UtcNow);
                        order.ApplyDiscount(coupon.Code, discount);
                        coupon.MarkUsed(order.Subtotal, UtcNow);
                        await data.AddAuditAsync(user.Id, "coupon.used", nameof(Coupon), coupon.Id, $"{coupon.Code} used on order {order.OrderNumber} for {discount:C}.", ct);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new ValidationAppException(ex.Message);
                    }
                }

                order.MarkPaid();
                foreach (var line in order.Lines)
                {
                    var inventories = await data.FindInventoryByProductAsync(line.ProductId, ct);
                    var remaining = line.Quantity;
                    foreach (var item in inventories.Where(x => x.Reserved > 0))
                    {
                        var take = Math.Min(item.Reserved, remaining);
                        item.Commit(take);
                        remaining -= take;
                        if (remaining == 0) break;
                    }
                }

                cart.Clear();
                await data.AddOrderAsync(order, ct);
                await data.AddAuditAsync(user.Id, "order.checkout", nameof(Order), order.Id, $"Order {order.OrderNumber} placed for {order.Total:C}.", ct);
                await data.SaveChangesAsync(ct);
                return MapOrder(order);
            }
            catch (ConcurrencyAppException ex)
            {
                if (attempt == maxRetries)
                {
                    throw new ConflictAppException(
                        "The checkout could not be completed due to high concurrent traffic on inventory. Please try again.",
                        ex);
                }

                // Detach all tracked entities to ensure subsequent reload fetches entirely fresh database records
                data.ClearTracker();

                // Dynamic backoff delay with random jitter to allow concurrent transactions to settle
                await Task.Delay(Random.Shared.Next(15, 60) * attempt, ct);
            }
        }

        throw new ConflictAppException("The checkout could not be completed due to high concurrent traffic on inventory.");
    }

    public async Task<IReadOnlyCollection<OrderDto>> OrdersAsync(CurrentUser user, CancellationToken ct) =>
        (await data.ListOrdersForUserAsync(user.Id, ct)).Select(MapOrder).ToArray();

    public async Task AddWishlistAsync(CurrentUser user, Guid productId, CancellationToken ct)
    {
        _ = await data.FindProductAsync(productId, ct) ?? throw new NotFoundAppException("Product was not found.");
        if (!await data.WishlistContainsAsync(user.Id, productId, ct))
            await data.AddWishlistItemAsync(new WishlistItem(user.Id, productId), ct);
        await data.SaveChangesAsync(ct);
    }

    public async Task<WishlistDto> WishlistAsync(CurrentUser user, CancellationToken ct) =>
        new(await data.ListWishlistAsync(user.Id, ct));

    public async Task RemoveWishlistAsync(CurrentUser user, Guid productId, CancellationToken ct)
    {
        await data.RemoveWishlistItemAsync(user.Id, productId, ct);
        await data.SaveChangesAsync(ct);
    }

    public async Task<ReviewDto> ReviewAsync(CurrentUser user, ReviewRequest request, CancellationToken ct)
    {
        var account = await data.FindUserAsync(user.Id, ct) ?? throw new UnauthorizedAppException("User session is no longer valid.");
        var product = await data.FindProductAsync(request.ProductId, ct) ?? throw new NotFoundAppException("Product was not found.");
        if (await data.FindReviewAsync(user.Id, product.Id, ct) is not null)
            throw new ConflictAppException("You have already reviewed this product.");

        var verified = await data.HasCompletedOrderLineAsync(user.Id, product.Id, ct);
        var review = new ProductReview(user.Id, product.Id, request.Rating, request.Title, request.Body, verified);
        await data.AddReviewAsync(review, ct);
        product.RecalculateRating((await data.ListReviewsAsync(product.Id, ct)).Select(x => x.Rating).Append(review.Rating));
        await data.SaveChangesAsync(ct);
        return new ReviewDto(review.Id, product.Id, account.DisplayName, review.Rating, review.Title, review.Body, review.VerifiedPurchase, review.CreatedAt);
    }

    public Task<DashboardSnapshotDto> DashboardAsync(CancellationToken ct) => data.DashboardAsync(ct);

    public async Task<CouponDto> CreateCouponAsync(CouponRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<CouponDiscountKind>(request.Kind, true, out var kind))
            throw new ValidationAppException("Coupon kind must be Percentage or FixedAmount.");
        if (await data.FindCouponByCodeAsync(request.Code, ct) is not null)
            throw new ConflictAppException("Coupon code already exists.");

        Coupon coupon;
        try
        {
            coupon = new Coupon(request.Code, kind, request.Value, request.ExpiresAt, request.MinimumOrderAmount, request.UsageLimit);
        }
        catch (InvalidOperationException ex)
        {
            throw new ValidationAppException(ex.Message);
        }

        await data.AddCouponAsync(coupon, ct);
        await data.AddAuditAsync(null, "coupon.created", nameof(Coupon), coupon.Id, $"{coupon.Code} created with {coupon.Kind} discount.", ct);
        await data.SaveChangesAsync(ct);
        return MapCoupon(coupon);
    }

    public async Task<IReadOnlyCollection<CouponDto>> CouponsAsync(CancellationToken ct) =>
        (await data.ListCouponsAsync(ct)).Select(MapCoupon).ToArray();

    public Task<IReadOnlyCollection<AuditLogDto>> AuditLogsAsync(int take, CancellationToken ct) =>
        data.ListAuditLogsAsync(Math.Clamp(take, 1, 200), ct);

    private async Task<Cart> EnsureCartAsync(Guid shopperId, CancellationToken ct)
    {
        var cart = await data.FindCartAsync(shopperId, ct);
        if (cart is not null) return cart;

        cart = new Cart(shopperId);
        await data.AddCartAsync(cart, ct);
        await data.SaveChangesAsync(ct);
        return cart;
    }

    private async Task<CartDto> MapCartAsync(Cart cart, CancellationToken ct)
    {
        var lines = new List<CartLineDto>();
        foreach (var line in cart.Lines)
        {
            var product = await data.FindProductAsync(line.ProductId, ct);
            if (product is not null)
                lines.Add(new CartLineDto(product.Id, product.Sku, product.Name, line.Quantity, line.UnitPrice, line.Quantity * line.UnitPrice));
        }
        var subtotal = lines.Sum(x => x.LineTotal);
        var discount = Math.Min(cart.DiscountAmount, subtotal);
        return new CartDto(cart.Id, lines, subtotal, discount, subtotal - discount, "USD", cart.AppliedCouponCode);
    }

    private static OrderDto MapOrder(Order order) =>
        new(order.Id, order.OrderNumber, order.State.ToString(), order.Subtotal, order.DiscountAmount, order.CouponCode, order.ShippingFee, order.Tax, order.Total, order.Currency,
            order.Lines.Select(x => new OrderLineDto(x.ProductId, x.Sku, x.ProductName, x.Quantity, x.UnitPrice, x.LineTotal)).ToArray());

    private static CouponDto MapCoupon(Coupon coupon) =>
        new(coupon.Id, coupon.Code, coupon.Kind.ToString(), coupon.Value, coupon.ExpiresAt, coupon.MinimumOrderAmount, coupon.UsageLimit, coupon.TimesUsed, coupon.IsActive);
}
