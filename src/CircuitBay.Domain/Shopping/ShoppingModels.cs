using CircuitBay.Domain.Common;
using CircuitBay.Domain.Enums;

namespace CircuitBay.Domain.Shopping;

public sealed class Cart : BaseEntity
{
    private readonly List<CartLine> _lines = [];

    private Cart() { }

    public Cart(Guid shopperId) => ShopperId = shopperId;

    public Guid ShopperId { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; } = DateTimeOffset.UtcNow;
    public string? AppliedCouponCode { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public IReadOnlyCollection<CartLine> Lines => _lines;

    public void AddOrUpdate(Guid productId, int quantity, decimal unitPrice, string currency)
    {
        if (quantity <= 0) throw new InvalidOperationException("Cart quantity must be positive.");

        var line = _lines.FirstOrDefault(x => x.ProductId == productId);
        if (line is null)
            _lines.Add(new CartLine(Id, productId, quantity, unitPrice, currency));
        else
            line.ChangeQuantity(quantity, unitPrice);

        LastActivityAt = DateTimeOffset.UtcNow;
        AppliedCouponCode = null;
        DiscountAmount = 0;
        Touch();
    }

    public void Remove(Guid productId)
    {
        _lines.RemoveAll(x => x.ProductId == productId);
        AppliedCouponCode = null;
        DiscountAmount = 0;
        LastActivityAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Clear()
    {
        _lines.Clear();
        AppliedCouponCode = null;
        DiscountAmount = 0;
        LastActivityAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void ApplyCoupon(string code, decimal discountAmount)
    {
        AppliedCouponCode = code.Trim().ToUpperInvariant();
        DiscountAmount = Math.Round(Math.Max(0, discountAmount), 2);
        LastActivityAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void ClearCoupon()
    {
        AppliedCouponCode = null;
        DiscountAmount = 0;
        LastActivityAt = DateTimeOffset.UtcNow;
        Touch();
    }
}

public sealed class CartLine : BaseEntity
{
    private CartLine() { }

    public CartLine(Guid cartId, Guid productId, int quantity, decimal unitPrice, string currency)
    {
        CartId = cartId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Currency = currency;
    }

    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = "USD";

    public void ChangeQuantity(int quantity, decimal currentUnitPrice)
    {
        if (quantity <= 0) throw new InvalidOperationException("Cart quantity must be positive.");
        Quantity = quantity;
        UnitPrice = currentUnitPrice;
        Touch();
    }
}

public sealed class Order : BaseEntity
{
    private readonly List<OrderLine> _lines = [];

    private Order() { }

    public Order(Guid shopperId, string email, string shippingName, string shippingAddress)
    {
        ShopperId = shopperId;
        CustomerEmail = email.Trim().ToLowerInvariant();
        ShippingName = shippingName.Trim();
        ShippingAddress = shippingAddress.Trim();
        State = OrderState.AwaitingPayment;
    }

    public Guid ShopperId { get; private set; }
    public string OrderNumber { get; private set; } = $"CB-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
    public string CustomerEmail { get; private set; } = string.Empty;
    public string ShippingName { get; private set; } = string.Empty;
    public string ShippingAddress { get; private set; } = string.Empty;
    public OrderState State { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string? CouponCode { get; private set; }
    public decimal ShippingFee { get; private set; }
    public decimal Tax { get; private set; }
    public decimal Total { get; private set; }
    public string Currency { get; private set; } = "USD";
    public IReadOnlyCollection<OrderLine> Lines => _lines;

    public void AddLine(Guid productId, string sku, string name, int quantity, decimal unitPrice, string currency)
    {
        if (State != OrderState.AwaitingPayment) throw new InvalidOperationException("Only unpaid orders can be edited.");
        if (quantity <= 0) throw new InvalidOperationException("Order line quantity must be positive.");
        Currency = currency;
        _lines.Add(new OrderLine(Id, productId, sku, name, quantity, unitPrice));
        Reprice();
    }

    public void MarkPaid()
    {
        if (!_lines.Any()) throw new InvalidOperationException("Cannot pay an empty order.");
        State = OrderState.Paid;
        Touch();
    }

    public void ApplyDiscount(string couponCode, decimal discountAmount)
    {
        if (State != OrderState.AwaitingPayment) throw new InvalidOperationException("Only unpaid orders can be discounted.");
        CouponCode = couponCode.Trim().ToUpperInvariant();
        DiscountAmount = Math.Round(Math.Max(0, Math.Min(discountAmount, Subtotal)), 2);
        Reprice();
    }

    public void Cancel()
    {
        if (State is OrderState.Shipped or OrderState.Completed)
            throw new InvalidOperationException("Shipped orders require a return workflow.");

        State = OrderState.Cancelled;
        Touch();
    }

    public void StartPacking()
    {
        if (State == OrderState.Paid)
        {
            State = OrderState.Packing;
            Touch();
        }
    }

    private void Reprice()
    {
        Subtotal = _lines.Sum(x => x.LineTotal);
        var taxable = Math.Max(0, Subtotal - DiscountAmount);
        ShippingFee = taxable >= 250 ? 0 : 12.50m;
        Tax = Math.Round(taxable * 0.0825m, 2);
        Total = taxable + ShippingFee + Tax;
    }
}

public sealed class OrderLine : BaseEntity
{
    private OrderLine() { }

    public OrderLine(Guid orderId, Guid productId, string sku, string productName, int quantity, decimal unitPrice)
    {
        OrderId = orderId;
        ProductId = productId;
        Sku = sku;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public sealed class WishlistItem : BaseEntity
{
    private WishlistItem() { }

    public WishlistItem(Guid shopperId, Guid productId)
    {
        ShopperId = shopperId;
        ProductId = productId;
    }

    public Guid ShopperId { get; private set; }
    public Guid ProductId { get; private set; }
}

public sealed class ProductReview : BaseEntity
{
    private ProductReview() { }

    public ProductReview(Guid shopperId, Guid productId, int rating, string title, string body, bool verifiedPurchase)
    {
        if (rating is < 1 or > 5) throw new InvalidOperationException("Rating must be from 1 to 5.");
        ShopperId = shopperId;
        ProductId = productId;
        Rating = rating;
        Title = title.Trim();
        Body = body.Trim();
        VerifiedPurchase = verifiedPurchase;
    }

    public Guid ShopperId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Rating { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public bool VerifiedPurchase { get; private set; }
    public bool IsHidden { get; private set; }
}
