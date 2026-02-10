using CircuitBay.Domain.Common;

namespace CircuitBay.Domain.Inventory;

public sealed class InventoryItem : BaseEntity
{
    private InventoryItem() { }

    public InventoryItem(Guid productId, string warehouseCode, int available, int reorderPoint)
    {
        ProductId = productId;
        WarehouseCode = warehouseCode.Trim().ToUpperInvariant();
        Available = available;
        ReorderPoint = reorderPoint;
    }

    public Guid ProductId { get; private set; }
    public string WarehouseCode { get; private set; } = string.Empty;
    public int Available { get; private set; }
    public int Reserved { get; private set; }
    public int ReorderPoint { get; private set; }
    public int Sellable => Math.Max(0, Available - Reserved);
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public void Receive(int quantity)
    {
        if (quantity <= 0) throw new InvalidOperationException("Receive quantity must be positive.");
        Available += quantity;
        Touch();
    }

    public void Reserve(int quantity)
    {
        if (quantity <= 0) throw new InvalidOperationException("Reserve quantity must be positive.");
        if (Sellable < quantity) throw new InvalidOperationException("Insufficient sellable inventory.");
        Reserved += quantity;
        Touch();
    }

    public void Release(int quantity)
    {
        Reserved = Math.Max(0, Reserved - quantity);
        Touch();
    }

    public void Commit(int quantity)
    {
        if (Reserved < quantity) throw new InvalidOperationException("Cannot commit more than reserved inventory.");
        Reserved -= quantity;
        Available -= quantity;
        Touch();
    }
}
