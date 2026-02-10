using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircuitBay.Infrastructure.Migrations;

[DbContext(typeof(Persistence.CircuitBayDbContext))]
[Migration("20260512013000_FilterUniqueIndexesForSoftDelete")]
public partial class FilterUniqueIndexesForSoftDelete : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        DropUniqueIndexes(migrationBuilder);
        migrationBuilder.CreateIndex("IX_categories_Slug", "categories", "Slug", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_cart_lines_CartId_ProductId", "cart_lines", new[] { "CartId", "ProductId" }, unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_carts_ShopperId", "carts", "ShopperId", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_inventory_items_ProductId_WarehouseCode", "inventory_items", new[] { "ProductId", "WarehouseCode" }, unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_orders_OrderNumber", "orders", "OrderNumber", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_product_reviews_ShopperId_ProductId", "product_reviews", new[] { "ShopperId", "ProductId" }, unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_products_Sku", "products", "Sku", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_products_Slug", "products", "Slug", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_shopper_accounts_Email", "shopper_accounts", "Email", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_wishlist_items_ShopperId_ProductId", "wishlist_items", new[] { "ShopperId", "ProductId" }, unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_coupons_Code", "coupons", "Code", unique: true, filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropUniqueIndexes(migrationBuilder);
        migrationBuilder.CreateIndex("IX_categories_Slug", "categories", "Slug", unique: true, filter: "[Slug] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_cart_lines_CartId_ProductId", "cart_lines", new[] { "CartId", "ProductId" }, unique: true, filter: "[CartId] IS NOT NULL AND [ProductId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_carts_ShopperId", "carts", "ShopperId", unique: true, filter: "[ShopperId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_inventory_items_ProductId_WarehouseCode", "inventory_items", new[] { "ProductId", "WarehouseCode" }, unique: true, filter: "[ProductId] IS NOT NULL AND [WarehouseCode] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_orders_OrderNumber", "orders", "OrderNumber", unique: true, filter: "[OrderNumber] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_product_reviews_ShopperId_ProductId", "product_reviews", new[] { "ShopperId", "ProductId" }, unique: true, filter: "[ShopperId] IS NOT NULL AND [ProductId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_products_Sku", "products", "Sku", unique: true, filter: "[Sku] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_products_Slug", "products", "Slug", unique: true, filter: "[Slug] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_shopper_accounts_Email", "shopper_accounts", "Email", unique: true, filter: "[Email] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_wishlist_items_ShopperId_ProductId", "wishlist_items", new[] { "ShopperId", "ProductId" }, unique: true, filter: "[ShopperId] IS NOT NULL AND [ProductId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_coupons_Code", "coupons", "Code", unique: true);
    }

    private static void DropUniqueIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_categories_Slug", "categories");
        migrationBuilder.DropIndex("IX_cart_lines_CartId_ProductId", "cart_lines");
        migrationBuilder.DropIndex("IX_carts_ShopperId", "carts");
        migrationBuilder.DropIndex("IX_inventory_items_ProductId_WarehouseCode", "inventory_items");
        migrationBuilder.DropIndex("IX_orders_OrderNumber", "orders");
        migrationBuilder.DropIndex("IX_product_reviews_ShopperId_ProductId", "product_reviews");
        migrationBuilder.DropIndex("IX_products_Sku", "products");
        migrationBuilder.DropIndex("IX_products_Slug", "products");
        migrationBuilder.DropIndex("IX_shopper_accounts_Email", "shopper_accounts");
        migrationBuilder.DropIndex("IX_wishlist_items_ShopperId_ProductId", "wishlist_items");
        migrationBuilder.DropIndex("IX_coupons_Code", "coupons");
    }
}
