using System.Linq.Expressions;
using CircuitBay.Domain.Auditing;
using CircuitBay.Domain.Catalog;
using CircuitBay.Domain.Common;
using CircuitBay.Domain.Enums;
using CircuitBay.Domain.Inventory;
using CircuitBay.Domain.Promotions;
using CircuitBay.Domain.Shopping;
using CircuitBay.Domain.Users;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace CircuitBay.Infrastructure.Persistence;

public sealed class CircuitBayDbContext(DbContextOptions<CircuitBayDbContext> options) : DbContext(options)
{
    public DbSet<ShopperAccount> Users => Set<ShopperAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartLine> CartLines => Set<CartLine>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>().Where(x => x.State == EntityState.Deleted))
        {
            entry.Entity.SoftDelete();
            entry.State = EntityState.Modified;
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShopperAccount>(b =>
        {
            b.ToTable("shopper_accounts");
            b.HasIndex(x => x.Email).IsUnique().HasFilter("[IsDeleted] = 0");
            b.Property(x => x.Email).HasMaxLength(220);
            b.Property(x => x.DisplayName).HasMaxLength(120);
            b.Property(x => x.PasswordHash).HasMaxLength(600);
            b.PrimitiveCollection(x => x.Roles).ElementType().HasConversion<string>();
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => new { x.UserId, x.IsRevoked, x.ExpiresAt });
            b.Property(x => x.TokenHash).HasMaxLength(128);
            b.Property(x => x.IssuedIpAddress).HasMaxLength(64);
            b.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            // Not a BaseEntity — no soft delete; physical delete on cleanup
        });

        modelBuilder.Entity<PasswordResetToken>(b =>
        {
            b.ToTable("password_reset_tokens");
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => new { x.UserId, x.IsUsed });
            b.Property(x => x.TokenHash).HasMaxLength(128);
        });

        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("categories");
            b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
            b.Property(x => x.Name).HasMaxLength(120);
            b.Property(x => x.Slug).HasMaxLength(160);
            b.HasOne(x => x.ParentCategory).WithMany().HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("products");
            b.HasIndex(x => x.Sku).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
            b.Property(x => x.Sku).HasMaxLength(64);
            b.Property(x => x.Name).HasMaxLength(220);
            b.Property(x => x.Brand).HasMaxLength(120);
            b.Property(x => x.Slug).HasMaxLength(260);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.Price).HasPrecision(18, 2);
            b.Property(x => x.AverageRating).HasPrecision(4, 2);
            b.HasMany(x => x.Specifications).WithOne().HasForeignKey(x => x.ProductId);
            b.Navigation(x => x.Specifications).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ProductSpecification>(b =>
        {
            b.ToTable("product_specifications");
            b.Property(x => x.Name).HasMaxLength(100);
            b.Property(x => x.Value).HasMaxLength(500);
        });

        modelBuilder.Entity<InventoryItem>(b =>
        {
            b.ToTable("inventory_items");
            b.HasIndex(x => new { x.ProductId, x.WarehouseCode }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.Property(x => x.WarehouseCode).HasMaxLength(32);
            b.Property(x => x.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<Cart>(b =>
        {
            b.ToTable("carts");
            b.HasIndex(x => x.ShopperId).IsUnique().HasFilter("[IsDeleted] = 0");
            b.Property(x => x.AppliedCouponCode).HasMaxLength(48);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CartLine>(b =>
        {
            b.ToTable("cart_lines");
            b.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(3);
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasIndex(x => x.OrderNumber).IsUnique().HasFilter("[IsDeleted] = 0");
            b.Property(x => x.OrderNumber).HasMaxLength(32);
            b.Property(x => x.CustomerEmail).HasMaxLength(220);
            b.Property(x => x.ShippingName).HasMaxLength(140);
            b.Property(x => x.ShippingAddress).HasMaxLength(800);
            b.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.CouponCode).HasMaxLength(48);
            b.Property(x => x.Subtotal).HasPrecision(18, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.Property(x => x.ShippingFee).HasPrecision(18, 2);
            b.Property(x => x.Tax).HasPrecision(18, 2);
            b.Property(x => x.Total).HasPrecision(18, 2);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<OrderLine>(b =>
        {
            b.ToTable("order_lines");
            b.Property(x => x.Sku).HasMaxLength(64);
            b.Property(x => x.ProductName).HasMaxLength(220);
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<WishlistItem>(b =>
        {
            b.ToTable("wishlist_items");
            b.HasIndex(x => new { x.ShopperId, x.ProductId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<ProductReview>(b =>
        {
            b.ToTable("product_reviews");
            b.HasIndex(x => new { x.ShopperId, x.ProductId }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.Property(x => x.Title).HasMaxLength(160);
            b.Property(x => x.Body).HasMaxLength(2000);
        });

        modelBuilder.Entity<Coupon>(b =>
        {
            b.ToTable("coupons");
            b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
            b.Property(x => x.Code).HasMaxLength(48);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.Value).HasPrecision(18, 2);
            b.Property(x => x.MinimumOrderAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("audit_logs");
            b.Property(x => x.Action).HasMaxLength(80);
            b.Property(x => x.EntityName).HasMaxLength(120);
            b.Property(x => x.Summary).HasMaxLength(1000);
            b.HasIndex(x => new { x.Action, x.CreatedAt });
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var body = Expression.Equal(
                Expression.Property(parameter, nameof(BaseEntity.IsDeleted)),
                Expression.Constant(false));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
            modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.IsDeleted)).HasDefaultValue(false);
            modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
        }
    }
}
