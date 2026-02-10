namespace CircuitBay.Contracts.Operations;

public sealed record InventoryReceiptRequest(Guid ProductId, string WarehouseCode, int Quantity, int ReorderPoint);
public sealed record InventoryDto(Guid ProductId, string Sku, string Name, string WarehouseCode, int Available, int Reserved, int Sellable, int ReorderPoint);

public sealed record CartLineRequest(Guid ProductId, int Quantity);
public sealed record CartDto(Guid Id, IReadOnlyCollection<CartLineDto> Lines, decimal Subtotal, decimal DiscountAmount, decimal Total, string Currency, string? CouponCode);
public sealed record CartLineDto(Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record ApplyCouponRequest(string Code);

public sealed record CheckoutRequest(string ShippingName, string ShippingAddress);
public sealed record OrderDto(Guid Id, string OrderNumber, string State, decimal Subtotal, decimal DiscountAmount, string? CouponCode, decimal ShippingFee, decimal Tax, decimal Total, string Currency, IReadOnlyCollection<OrderLineDto> Lines);
public sealed record OrderLineDto(Guid ProductId, string Sku, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record WishlistDto(IReadOnlyCollection<ProductTinyDto> Items);
public sealed record ProductTinyDto(Guid Id, string Sku, string Name, decimal Price, string Currency);

public sealed record ReviewRequest(Guid ProductId, int Rating, string Title, string Body);
public sealed record ReviewDto(Guid Id, Guid ProductId, string Author, int Rating, string Title, string Body, bool VerifiedPurchase, DateTimeOffset CreatedAt);

public sealed record DashboardSnapshotDto(int PublishedProducts, int LowStockItems, int OpenOrders, decimal Revenue30Days, IReadOnlyCollection<string> OperationalNotes);

public sealed record CouponRequest(string Code, string Kind, decimal Value, DateTimeOffset ExpiresAt, decimal MinimumOrderAmount, int UsageLimit);
public sealed record CouponDto(Guid Id, string Code, string Kind, decimal Value, DateTimeOffset ExpiresAt, decimal MinimumOrderAmount, int UsageLimit, int TimesUsed, bool IsActive);
public sealed record AuditLogDto(Guid Id, Guid? ActorId, string Action, string EntityName, Guid? EntityId, string Summary, DateTimeOffset CreatedAt);
