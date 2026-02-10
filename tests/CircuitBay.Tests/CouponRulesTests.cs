using CircuitBay.Application.Common;
using CircuitBay.Application.Services;
using CircuitBay.Contracts.Operations;
using CircuitBay.Domain.Catalog;
using CircuitBay.Domain.Inventory;
using CircuitBay.Domain.Promotions;
using CircuitBay.Domain.Users;

namespace CircuitBay.Tests;

public sealed class CouponRulesTests
{
    [Fact]
    public async Task Percentage_coupon_recalculates_cart_total()
    {
        var (store, user, product) = await BuildPricedCartAsync(200);
        await store.AddCouponAsync(new Coupon("LAB10", CouponDiscountKind.Percentage, 10, DateTimeOffset.UtcNow.AddDays(3), 100, 5), CancellationToken.None);
        var service = new CommerceService(store);
        var current = new CurrentUser(user.Id, user.Email, ["Shopper"]);

        await service.PutCartLineAsync(current, new CartLineRequest(product.Id, 1), CancellationToken.None);
        var cart = await service.ApplyCouponAsync(current, new ApplyCouponRequest("lab10"), CancellationToken.None);

        Assert.Equal(200, cart.Subtotal);
        Assert.Equal(20, cart.DiscountAmount);
        Assert.Equal(180, cart.Total);
        Assert.Equal("LAB10", cart.CouponCode);
    }

    [Fact]
    public async Task Coupon_minimum_order_amount_is_enforced()
    {
        var (store, user, product) = await BuildPricedCartAsync(80);
        await store.AddCouponAsync(new Coupon("BIGSAVE", CouponDiscountKind.FixedAmount, 25, DateTimeOffset.UtcNow.AddDays(3), 100, 5), CancellationToken.None);
        var service = new CommerceService(store);
        var current = new CurrentUser(user.Id, user.Email, ["Shopper"]);
        await service.PutCartLineAsync(current, new CartLineRequest(product.Id, 1), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.ApplyCouponAsync(current, new ApplyCouponRequest("BIGSAVE"), CancellationToken.None));
    }

    [Fact]
    public async Task Checkout_marks_coupon_usage_and_reduces_order_total()
    {
        var (store, user, product) = await BuildPricedCartAsync(300);
        var coupon = new Coupon("SIGNAL25", CouponDiscountKind.FixedAmount, 25, DateTimeOffset.UtcNow.AddDays(3), 100, 1);
        await store.AddCouponAsync(coupon, CancellationToken.None);
        var service = new CommerceService(store);
        var current = new CurrentUser(user.Id, user.Email, ["Shopper"]);

        await service.PutCartLineAsync(current, new CartLineRequest(product.Id, 1), CancellationToken.None);
        await service.ApplyCouponAsync(current, new ApplyCouponRequest("SIGNAL25"), CancellationToken.None);
        var order = await service.CheckoutAsync(current, new CheckoutRequest("Mira Chen", "42 Workbench Lane"), CancellationToken.None);

        Assert.Equal("SIGNAL25", order.CouponCode);
        Assert.Equal(25, order.DiscountAmount);
        Assert.False(coupon.IsActive);
        Assert.Equal(1, coupon.TimesUsed);
    }

    private static async Task<(MemoryStore Store, ShopperAccount User, Product Product)> BuildPricedCartAsync(decimal price)
    {
        var store = new MemoryStore();
        var category = await store.CreateCategoryAsync("Components");
        var product = new Product($"CB-ITEM-{price:0}", "Bench Component", "SignalNest", $"bench-component-{price:0}", category.Id, price, "USD");
        product.ReviseDetails("Bench-ready component", price);
        product.Publish();
        await store.AddProductAsync(product, CancellationToken.None);
        await store.AddInventoryAsync(new InventoryItem(product.Id, "DFW-A", 5, 1), CancellationToken.None);
        var user = new ShopperAccount("mira@example.com", "Mira", "hash");
        await store.AddUserAsync(user, CancellationToken.None);
        return (store, user, product);
    }
}
