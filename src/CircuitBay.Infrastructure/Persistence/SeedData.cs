using CircuitBay.Domain.Catalog;
using CircuitBay.Domain.Enums;
using CircuitBay.Domain.Inventory;
using CircuitBay.Domain.Promotions;
using CircuitBay.Domain.Users;

namespace CircuitBay.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task SeedAsync(CircuitBayDbContext db, Func<string, string> hashPassword, CancellationToken ct = default)
    {
        if (db.Users.Any())
        {
            if (!db.Coupons.Any())
            {
                db.Coupons.AddRange(
                    new Coupon("LABBENCH10", CouponDiscountKind.Percentage, 10, DateTimeOffset.UtcNow.AddMonths(3), 100, 500),
                    new Coupon("SIGNAL25", CouponDiscountKind.FixedAmount, 25, DateTimeOffset.UtcNow.AddMonths(1), 150, 120));
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        var laptops = new Category("Laptops", "laptops");
        var components = new Category("Components", "components");
        var audio = new Category("Audio", "audio");
        db.Categories.AddRange(laptops, components, audio);

        var admin = new ShopperAccount("owner@circuitbay.local", "CircuitBay Ops", hashPassword("OwnerPass!2026"));
        admin.AddRole(UserRole.Admin);
        admin.AddRole(UserRole.InventoryManager);
        var shopper = new ShopperAccount("mira.chen@example.com", "Mira Chen", hashPassword("ShopperPass!2026"));
        db.Users.AddRange(admin, shopper);

        var products = new[]
        {
            NewProduct("CB-LP-1407", "Kestrel 14 Pro OLED", "Northbridge", "kestrel-14-pro-oled", laptops.Id, 1299.00m, "14-inch OLED ultrabook with repairable memory cage.", ("Panel", "2.8K OLED"), ("Memory", "32 GB")),
            NewProduct("CB-GPU-7800", "VoltForge RX 7800 XT Silent", "VoltForge", "voltforge-rx-7800-xt-silent", components.Id, 519.00m, "Triple-fan graphics card tuned for quiet creator workstations.", ("VRAM", "16 GB"), ("Length", "302 mm")),
            NewProduct("CB-SSD-4T", "QuantaVault NVMe 4TB", "QuantaVault", "quantavault-nvme-4tb", components.Id, 309.00m, "PCIe Gen4 drive with sustained write cooling plate.", ("Interface", "PCIe 4.0"), ("Endurance", "2400 TBW")),
            NewProduct("CB-DAC-MINI", "SignalNest USB-C DAC", "SignalNest", "signalnest-usb-c-dac", audio.Id, 89.00m, "Pocket DAC with hardware gain switch and low-noise floor.", ("Output", "3.5 mm"), ("Chipset", "CS43131"))
        };
        db.Products.AddRange(products);
        db.Inventory.AddRange(
            new InventoryItem(products[0].Id, "DFW-A", 11, 4),
            new InventoryItem(products[1].Id, "DFW-A", 7, 3),
            new InventoryItem(products[2].Id, "PHX-B", 22, 8),
            new InventoryItem(products[3].Id, "DFW-A", 3, 6));
        db.Coupons.AddRange(
            new Coupon("LABBENCH10", CouponDiscountKind.Percentage, 10, DateTimeOffset.UtcNow.AddMonths(3), 100, 500),
            new Coupon("SIGNAL25", CouponDiscountKind.FixedAmount, 25, DateTimeOffset.UtcNow.AddMonths(1), 150, 120));

        await db.SaveChangesAsync(ct);
    }

    private static Product NewProduct(string sku, string name, string brand, string slug, Guid categoryId, decimal price, string description, params (string Name, string Value)[] specs)
    {
        var product = new Product(sku, name, brand, slug, categoryId, price, "USD");
        product.ReviseDetails(description, price);
        foreach (var spec in specs)
            product.AddSpecification(spec.Name, spec.Value);
        product.Publish();
        return product;
    }
}
