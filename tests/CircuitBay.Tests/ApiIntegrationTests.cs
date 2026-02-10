using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CircuitBay.Contracts.Auth;
using CircuitBay.Contracts.Operations;
using CircuitBay.Domain.Catalog;
using CircuitBay.Domain.Inventory;
using CircuitBay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CircuitBay.Tests;

[CollectionDefinition("Integration")]
public class IntegrationCollection
    : ICollectionFixture<CircuitBayApiFixture>
{
}

[Collection("Integration")]
public sealed class ApiIntegrationTests
{
    private readonly CircuitBayApiFixture _fixture;

    public ApiIntegrationTests(CircuitBayApiFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient CreateClient()
    {
        return _fixture.CreateClient();
    }

    private async Task<AuthResponse> RegisterAndLoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                email,
                "Test User",
                password));

        if (registerResponse.IsSuccessStatusCode)
        {
            var registerAuth =
                await registerResponse.Content
                    .ReadFromJsonAsync<AuthResponse>();

            return registerAuth!;
        }

        var registerBody =
            await registerResponse.Content.ReadAsStringAsync();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));

        if (!loginResponse.IsSuccessStatusCode)
        {
            var loginBody =
                await loginResponse.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Auth setup failed for {email}. Register {(int)registerResponse.StatusCode}: {registerBody}; Login {(int)loginResponse.StatusCode}: {loginBody}");
        }

        var loginAuth =
            await loginResponse.Content
                .ReadFromJsonAsync<AuthResponse>();

        return loginAuth!;
    }

    [Fact]
    public async Task Authentication_Lifecycle_Register_Login_Refresh()
    {
        var client = CreateClient();

        var email =
            $"user_{Guid.NewGuid():N}@example.com";

        var password = "StrongPassword123!";

        var auth =
            await RegisterAndLoginAsync(
                client,
                email,
                password);

        Assert.NotNull(auth.AccessToken);
        Assert.NotNull(auth.RefreshToken);

        var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/cart");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth.AccessToken);

        var cartResponse =
            await client.SendAsync(request);

        cartResponse.EnsureSuccessStatusCode();

        var refreshResponse =
            await client.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshRequest(auth.RefreshToken));

        refreshResponse.EnsureSuccessStatusCode();

        var refreshed =
            await refreshResponse.Content
                .ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(refreshed);
        Assert.NotEqual(
            auth.AccessToken,
            refreshed!.AccessToken);

        var oldRefreshResponse =
            await client.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshRequest(auth.RefreshToken));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            oldRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_Authorization_Restrictions_Are_Enforced()
    {
        var shopperClient = CreateClient();

        var shopperAuth =
            await RegisterAndLoginAsync(
                shopperClient,
                $"shopper_{Guid.NewGuid():N}@example.com",
                "StrongPassword123!");

        var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/admin/dashboard");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                shopperAuth.AccessToken);

        var forbiddenResponse =
            await shopperClient.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenResponse.StatusCode);

        var adminClient = CreateClient();

        var adminAuth =
            await RegisterAndLoginAsync(
                adminClient,
                "owner@circuitbay.local",
                "OwnerPass!2026");

        var adminRequest =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/admin/dashboard");

        adminRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminAuth.AccessToken);

        var adminResponse =
            await adminClient.SendAsync(adminRequest);

        adminResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SoftDelete_Filtering_Behavior_Excludes_Deleted_Entities()
    {
        using var scope =
            _fixture.Services.CreateScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<CircuitBayDbContext>();

        var slug =
            $"test-delete-{Guid.NewGuid():N}";

        var category =
            new Category(
                "Delete Test Category",
                slug);

        db.Categories.Add(category);

        await db.SaveChangesAsync();

        Assert.NotNull(
            db.Categories
                .FirstOrDefault(x => x.Slug == slug));

        db.Categories.Remove(category);

        await db.SaveChangesAsync();

        Assert.Null(
            db.Categories
                .FirstOrDefault(x => x.Slug == slug));

        Assert.NotNull(
            db.Categories
                .IgnoreQueryFilters()
                .FirstOrDefault(x => x.Slug == slug));
    }

    [Fact]
    public async Task Checkout_Flow_Reduces_Inventory()
    {
        var adminClient = CreateClient();

        var adminAuth =
            await RegisterAndLoginAsync(
                adminClient,
                "owner@circuitbay.local",
                "OwnerPass!2026");

        Guid productId;

        using (var scope =
               _fixture.Services.CreateScope())
        {
            var db =
                scope.ServiceProvider
                    .GetRequiredService<CircuitBayDbContext>();

            var category =
                new Category(
                    $"Category-{Guid.NewGuid():N}",
                    $"category-{Guid.NewGuid():N}");

            db.Categories.Add(category);

            var product =
                new Product(
                    $"SKU-{Guid.NewGuid():N}",
                    "Checkout Product",
                    "Brand",
                    $"checkout-{Guid.NewGuid():N}",
                    category.Id,
                    100m,
                    "USD");

            product.Publish();

            db.Products.Add(product);

            await db.SaveChangesAsync();

            productId = product.Id;
        }

        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminAuth.AccessToken);

        var receiveResponse =
            await adminClient.PostAsJsonAsync(
                "/api/admin/inventory/receipts",
                new InventoryReceiptRequest(
                    productId,
                    "WH-1",
                    5,
                    2));

        receiveResponse.EnsureSuccessStatusCode();

        var shopperClient = CreateClient();

        var shopperAuth =
            await RegisterAndLoginAsync(
                shopperClient,
                $"shopper_{Guid.NewGuid():N}@example.com",
                "StrongPassword123!");

        shopperClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                shopperAuth.AccessToken);

        var cartResponse =
            await shopperClient.PutAsJsonAsync(
                "/api/cart/lines",
                new CartLineRequest(
                    productId,
                    2));

        cartResponse.EnsureSuccessStatusCode();

        var checkoutResponse =
            await shopperClient.PostAsJsonAsync(
                "/api/checkout",
                new CheckoutRequest(
                    "Test User",
                    "123 Test Street"));

        checkoutResponse.EnsureSuccessStatusCode();

        var order =
            await checkoutResponse.Content
                .ReadFromJsonAsync<OrderDto>();

        Assert.NotNull(order);

        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminAuth.AccessToken);

        var inventoryResponse =
            await adminClient.GetAsync(
                "/api/admin/inventory");

        inventoryResponse.EnsureSuccessStatusCode();

        var inventory =
            await inventoryResponse.Content
                .ReadFromJsonAsync<InventoryDto[]>();

        var item =
            inventory!
                .FirstOrDefault(x => x.ProductId == productId);

        Assert.NotNull(item);

        Assert.Equal(3, item!.Sellable);
    }

    [Fact]
    public async Task Coupon_Application_and_Removal()
    {
        var adminClient = CreateClient();

        var adminAuth =
            await RegisterAndLoginAsync(
                adminClient,
                "owner@circuitbay.local",
                "OwnerPass!2026");

        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminAuth.AccessToken);

        var couponCode =
            $"TEST-{Guid.NewGuid():N}"
                .Substring(0, 10)
                .ToUpper();

        var createCouponResponse =
            await adminClient.PostAsJsonAsync(
                "/api/admin/coupons",
                new CouponRequest(
                    couponCode,
                    "FixedAmount",
                    15m,
                    DateTimeOffset.UtcNow.AddDays(1),
                    50m,
                    10));

        createCouponResponse.EnsureSuccessStatusCode();

        Guid productId;

        using (var scope =
               _fixture.Services.CreateScope())
        {
            var db =
                scope.ServiceProvider
                    .GetRequiredService<CircuitBayDbContext>();

            var category =
                new Category(
                    $"CouponCat-{Guid.NewGuid():N}",
                    $"coupon-cat-{Guid.NewGuid():N}");

            db.Categories.Add(category);

            var product =
                new Product(
                    $"SKU-{Guid.NewGuid():N}",
                    "Coupon Product",
                    "Brand",
                    $"coupon-product-{Guid.NewGuid():N}",
                    category.Id,
                    100m,
                    "USD");

            product.Publish();

            db.Products.Add(product);

            await db.SaveChangesAsync();

            productId = product.Id;
        }

        var inventoryResponse =
            await adminClient.PostAsJsonAsync(
                "/api/admin/inventory/receipts",
                new InventoryReceiptRequest(
                    productId,
                    "WH-1",
                    5,
                    2));

        inventoryResponse.EnsureSuccessStatusCode();

        var shopperClient = CreateClient();

        var shopperAuth =
            await RegisterAndLoginAsync(
                shopperClient,
                $"shopper_{Guid.NewGuid():N}@example.com",
                "StrongPassword123!");

        shopperClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                shopperAuth.AccessToken);

        var cartResponse =
            await shopperClient.PutAsJsonAsync(
                "/api/cart/lines",
                new CartLineRequest(
                    productId,
                    1));

        cartResponse.EnsureSuccessStatusCode();

        var applyResponse =
            await shopperClient.PutAsJsonAsync(
                "/api/cart/coupon",
                new ApplyCouponRequest(couponCode));

        applyResponse.EnsureSuccessStatusCode();

        var cart =
            await applyResponse.Content
                .ReadFromJsonAsync<CartDto>();

        Assert.NotNull(cart);

        Assert.Equal(couponCode, cart!.CouponCode);

        var removeResponse =
            await shopperClient.DeleteAsync(
                "/api/cart/coupon");

        removeResponse.EnsureSuccessStatusCode();

        var removedCart =
            await removeResponse.Content
                .ReadFromJsonAsync<CartDto>();

        Assert.NotNull(removedCart);

        Assert.Null(removedCart!.CouponCode);
    }

    [Fact]
    public async Task Concurrent_Checkout_Prevents_Overselling_And_Returns_Conflict()
    {
        // 1. Seed limited product (1 unit in stock)
        Guid productId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CircuitBayDbContext>();
            var category = new Category($"CatConcurrency-{Guid.NewGuid():N}", $"cat-concurrency-{Guid.NewGuid():N}");
            db.Categories.Add(category);

            var product = new Product(
                $"SKU-{Guid.NewGuid():N}",
                "Concurrency Product",
                "Brand",
                $"concurrency-product-{Guid.NewGuid():N}",
                category.Id,
                150m,
                "USD");
            product.Publish();
            db.Products.Add(product);

            // Exactly 1 unit available in warehouse WH-1
            var inventory = new InventoryItem(product.Id, "WH-1", 1, 0);
            db.Inventory.Add(inventory);

            await db.SaveChangesAsync();
            productId = product.Id;
        }

        // 2. Setup Client A (Shopper A)
        var clientA = CreateClient();
        var authA = await RegisterAndLoginAsync(
            clientA,
            $"shopper_a_{Guid.NewGuid():N}@example.com",
            "StrongPassword123!");
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);

        // 3. Setup Client B (Shopper B)
        var clientB = CreateClient();
        var authB = await RegisterAndLoginAsync(
            clientB,
            $"shopper_b_{Guid.NewGuid():N}@example.com",
            "StrongPassword123!");
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB.AccessToken);

        // 4. Add product to both carts
        var cartA = await clientA.PutAsJsonAsync("/api/cart/lines", new CartLineRequest(productId, 1));
        cartA.EnsureSuccessStatusCode();

        var cartB = await clientB.PutAsJsonAsync("/api/cart/lines", new CartLineRequest(productId, 1));
        cartB.EnsureSuccessStatusCode();

        // 5. Trigger concurrent checkouts
        var checkoutReqA = new CheckoutRequest("Shopper A", "100 A Street");
        var checkoutReqB = new CheckoutRequest("Shopper B", "200 B Street");

        // We launch both checkouts at the same time to induce a race condition on the single inventory row
        var taskA = clientA.PostAsJsonAsync("/api/checkout", checkoutReqA);
        var taskB = clientB.PostAsJsonAsync("/api/checkout", checkoutReqB);

        await Task.WhenAll(taskA, taskB);

        var responseA = await taskA;
        var responseB = await taskB;

        // 6. Verify that exactly one succeeded and the other returned 409 Conflict
        var successCount = 0;
        var conflictCount = 0;

        if (responseA.StatusCode == HttpStatusCode.OK || responseA.StatusCode == HttpStatusCode.Created) successCount++;
        else if (responseA.StatusCode == HttpStatusCode.Conflict) conflictCount++;

        if (responseB.StatusCode == HttpStatusCode.OK || responseB.StatusCode == HttpStatusCode.Created) successCount++;
        else if (responseB.StatusCode == HttpStatusCode.Conflict) conflictCount++;

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);

        // 7. Verify inventory in database remains exactly 0 available and 0 reserved, total sellable = 0
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CircuitBayDbContext>();
            var item = await db.Inventory.FirstAsync(x => x.ProductId == productId);
            
            Assert.Equal(0, item.Available);
            Assert.Equal(0, item.Reserved);
            Assert.Equal(0, item.Sellable);
        }
    }
}
