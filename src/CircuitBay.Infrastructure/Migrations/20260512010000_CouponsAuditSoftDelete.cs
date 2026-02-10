using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircuitBay.Infrastructure.Migrations;

[DbContext(typeof(Persistence.CircuitBayDbContext))]
[Migration("20260512010000_CouponsAuditSoftDelete")]
public partial class CouponsAuditSoftDelete : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[]
        {
            "categories", "shopper_accounts", "products", "carts", "orders", "cart_lines", "inventory_items",
            "order_lines", "product_reviews", "product_specifications", "wishlist_items"
        })
        {
            migrationBuilder.AddColumn<bool>(name: "IsDeleted", table: table, type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "DeletedAt", table: table, type: "datetimeoffset", nullable: true);
        }

        migrationBuilder.AddColumn<string>(name: "AppliedCouponCode", table: "carts", type: "nvarchar(48)", maxLength: 48, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "DiscountAmount", table: "carts", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>(name: "CouponCode", table: "orders", type: "nvarchar(48)", maxLength: 48, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "DiscountAmount", table: "orders", type: "decimal(18,2)", nullable: false, defaultValue: 0m);

        migrationBuilder.CreateTable(
            name: "coupons",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                MinimumOrderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                UsageLimit = table.Column<int>(type: "int", nullable: false),
                TimesUsed = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_coupons", x => x.Id));

        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                EntityName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_audit_logs", x => x.Id));

        migrationBuilder.CreateIndex("IX_coupons_Code", "coupons", "Code", unique: true);
        migrationBuilder.CreateIndex("IX_audit_logs_Action_CreatedAt", "audit_logs", new[] { "Action", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("audit_logs");
        migrationBuilder.DropTable("coupons");
        migrationBuilder.DropColumn("AppliedCouponCode", "carts");
        migrationBuilder.DropColumn("DiscountAmount", "carts");
        migrationBuilder.DropColumn("CouponCode", "orders");
        migrationBuilder.DropColumn("DiscountAmount", "orders");

        foreach (var table in new[]
        {
            "categories", "shopper_accounts", "products", "carts", "orders", "cart_lines", "inventory_items",
            "order_lines", "product_reviews", "product_specifications", "wishlist_items"
        })
        {
            migrationBuilder.DropColumn("IsDeleted", table);
            migrationBuilder.DropColumn("DeletedAt", table);
        }
    }
}
