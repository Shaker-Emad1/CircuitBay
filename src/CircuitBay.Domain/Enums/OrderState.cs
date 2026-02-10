namespace CircuitBay.Domain.Enums;

public enum OrderState
{
    Draft = 0,
    AwaitingPayment = 1,
    Paid = 2,
    Packing = 3,
    Shipped = 4,
    Completed = 5,
    Cancelled = 6,
    Refunded = 7
}
