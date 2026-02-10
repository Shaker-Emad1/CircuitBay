using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace CircuitBay.Infrastructure.Migrations;

[DbContext(typeof(Persistence.CircuitBayDbContext))]
[Migration("20260512000000_InitialCircuitBay")]
public partial class InitialCircuitBay : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "categories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                ParentCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_categories", x => x.Id);
                table.ForeignKey("FK_categories_categories_ParentCategoryId", x => x.ParentCategoryId, "categories", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "shopper_accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Email = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                IsLocked = table.Column<bool>(type: "bit", nullable: false),
                LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Roles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_shopper_accounts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                Brand = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Slug = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                IsPublished = table.Column<bool>(type: "bit", nullable: false),
                AverageRating = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                ReviewCount = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_products", x => x.Id);
                table.ForeignKey("FK_products_categories_CategoryId", x => x.CategoryId, "categories", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "carts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShopperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LastActivityAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_carts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShopperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrderNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CustomerEmail = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                ShippingName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                ShippingAddress = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ShippingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Tax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_orders", x => x.Id));

        migrationBuilder.CreateTable(
            name: "cart_lines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cart_lines", x => x.Id);
                table.ForeignKey("FK_cart_lines_carts_CartId", x => x.CartId, "carts", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "inventory_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WarehouseCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Available = table.Column<int>(type: "int", nullable: false),
                Reserved = table.Column<int>(type: "int", nullable: false),
                ReorderPoint = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_inventory_items", x => x.Id));

        migrationBuilder.CreateTable(
            name: "order_lines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ProductName = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_lines", x => x.Id);
                table.ForeignKey("FK_order_lines_orders_OrderId", x => x.OrderId, "orders", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "product_reviews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShopperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Rating = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                VerifiedPurchase = table.Column<bool>(type: "bit", nullable: false),
                IsHidden = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_product_reviews", x => x.Id));

        migrationBuilder.CreateTable(
            name: "product_specifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_specifications", x => x.Id);
                table.ForeignKey("FK_product_specifications_products_ProductId", x => x.ProductId, "products", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "wishlist_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShopperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_wishlist_items", x => x.Id));

        migrationBuilder.CreateIndex("IX_categories_ParentCategoryId", "categories", "ParentCategoryId");
        migrationBuilder.CreateIndex("IX_categories_Slug", "categories", "Slug", unique: true);
        migrationBuilder.CreateIndex("IX_cart_lines_CartId_ProductId", "cart_lines", new[] { "CartId", "ProductId" }, unique: true);
        migrationBuilder.CreateIndex("IX_carts_ShopperId", "carts", "ShopperId", unique: true);
        migrationBuilder.CreateIndex("IX_inventory_items_ProductId_WarehouseCode", "inventory_items", new[] { "ProductId", "WarehouseCode" }, unique: true);
        migrationBuilder.CreateIndex("IX_order_lines_OrderId", "order_lines", "OrderId");
        migrationBuilder.CreateIndex("IX_orders_OrderNumber", "orders", "OrderNumber", unique: true);
        migrationBuilder.CreateIndex("IX_product_reviews_ShopperId_ProductId", "product_reviews", new[] { "ShopperId", "ProductId" }, unique: true);
        migrationBuilder.CreateIndex("IX_product_specifications_ProductId", "product_specifications", "ProductId");
        migrationBuilder.CreateIndex("IX_products_CategoryId", "products", "CategoryId");
        migrationBuilder.CreateIndex("IX_products_Sku", "products", "Sku", unique: true);
        migrationBuilder.CreateIndex("IX_products_Slug", "products", "Slug", unique: true);
        migrationBuilder.CreateIndex("IX_shopper_accounts_Email", "shopper_accounts", "Email", unique: true);
        migrationBuilder.CreateIndex("IX_wishlist_items_ShopperId_ProductId", "wishlist_items", new[] { "ShopperId", "ProductId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("cart_lines");
        migrationBuilder.DropTable("inventory_items");
        migrationBuilder.DropTable("order_lines");
        migrationBuilder.DropTable("product_reviews");
        migrationBuilder.DropTable("product_specifications");
        migrationBuilder.DropTable("shopper_accounts");
        migrationBuilder.DropTable("wishlist_items");
        migrationBuilder.DropTable("carts");
        migrationBuilder.DropTable("orders");
        migrationBuilder.DropTable("products");
        migrationBuilder.DropTable("categories");
    }
}
