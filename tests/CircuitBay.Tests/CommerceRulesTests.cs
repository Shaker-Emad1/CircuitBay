using CircuitBay.Application.Common;
using CircuitBay.Application.Services;
using CircuitBay.Contracts.Operations;
using CircuitBay.Domain.Catalog;
using CircuitBay.Domain.Inventory;
using CircuitBay.Domain.Users;

namespace CircuitBay.Tests;

public sealed class CommerceRulesTests
{
    [Fact]
    public async Task Cart_rejects_more_than_available_inventory()
    {
        var store = new MemoryStore();
        var category = await store.CreateCategoryAsync("Components");
        var product = new Product("CB-SSD-1T", "QuantaVault 1TB", "QuantaVault", "quantavault-1tb", category.Id, 120, "USD");
        product.ReviseDetails("Fast drive", 120);
        product.Publish();
        await store.AddProductAsync(product, CancellationToken.None);
        await store.AddInventoryAsync(new InventoryItem(product.Id, "DFW-A", 1, 3), CancellationToken.None);
        var user = new ShopperAccount("mira@example.com", "Mira", "hash");
        await store.AddUserAsync(user, CancellationToken.None);

        var service = new CommerceService(store);

        await Assert.ThrowsAsync<ConflictAppException>(() =>
            service.PutCartLineAsync(new CurrentUser(user.Id, user.Email, ["Shopper"]), new CartLineRequest(product.Id, 2), CancellationToken.None));
    }

    [Fact]
    public async Task Checkout_commits_reserved_inventory_and_clears_cart()
    {
        var store = new MemoryStore();
        var category = await store.CreateCategoryAsync("Audio");
        var product = new Product("CB-DAC-MINI", "SignalNest DAC", "SignalNest", "signalnest-dac", category.Id, 89, "USD");
        product.ReviseDetails("Small DAC", 89);
        product.Publish();
        await store.AddProductAsync(product, CancellationToken.None);
        var inventory = new InventoryItem(product.Id, "DFW-A", 3, 1);
        await store.AddInventoryAsync(inventory, CancellationToken.None);
        var user = new ShopperAccount("mira@example.com", "Mira", "hash");
        await store.AddUserAsync(user, CancellationToken.None);
        var service = new CommerceService(store);
        var current = new CurrentUser(user.Id, user.Email, ["Shopper"]);

        await service.PutCartLineAsync(current, new CartLineRequest(product.Id, 2), CancellationToken.None);
        var order = await service.CheckoutAsync(current, new CheckoutRequest("Mira Chen", "42 Workbench Lane"), CancellationToken.None);

        Assert.Equal("Paid", order.State);
        Assert.Equal(1, inventory.Sellable);
        Assert.Empty((await service.GetCartAsync(current, CancellationToken.None)).Lines);
    }
}
