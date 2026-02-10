using CircuitBay.Domain.Common;

namespace CircuitBay.Domain.Promotions;

public enum CouponDiscountKind
{
    Percentage = 0,
    FixedAmount = 1
}

public sealed class Coupon : BaseEntity
{
    private Coupon() { }

    public Coupon(string code, CouponDiscountKind kind, decimal value, DateTimeOffset expiresAt, decimal minimumOrderAmount, int usageLimit)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Coupon code is required.");
        if (value <= 0) throw new InvalidOperationException("Coupon value must be positive.");
        if (kind == CouponDiscountKind.Percentage && value > 80) throw new InvalidOperationException("Percentage coupons cannot exceed 80%.");
        if (usageLimit <= 0) throw new InvalidOperationException("Usage limit must be positive.");

        Code = code.Trim().ToUpperInvariant();
        Kind = kind;
        Value = value;
        ExpiresAt = expiresAt;
        MinimumOrderAmount = minimumOrderAmount;
        UsageLimit = usageLimit;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public CouponDiscountKind Kind { get; private set; }
    public decimal Value { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public decimal MinimumOrderAmount { get; private set; }
    public int UsageLimit { get; private set; }
    public int TimesUsed { get; private set; }
    public bool IsActive { get; private set; }

    public decimal CalculateDiscount(decimal subtotal, DateTimeOffset now)
    {
        EnsureUsable(subtotal, now);
        var raw = Kind == CouponDiscountKind.Percentage ? subtotal * (Value / 100m) : Value;
        return Math.Round(Math.Min(raw, subtotal), 2);
    }

    public void MarkUsed(decimal subtotal, DateTimeOffset now)
    {
        EnsureUsable(subtotal, now);
        TimesUsed++;
        if (TimesUsed >= UsageLimit)
            IsActive = false;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    private void EnsureUsable(decimal subtotal, DateTimeOffset now)
    {
        if (!IsActive) throw new InvalidOperationException("Coupon is not active.");
        if (ExpiresAt <= now) throw new InvalidOperationException("Coupon is expired.");
        if (TimesUsed >= UsageLimit) throw new InvalidOperationException("Coupon usage limit has been reached.");
        if (subtotal < MinimumOrderAmount) throw new InvalidOperationException("Cart subtotal does not meet the coupon minimum.");
    }
}
