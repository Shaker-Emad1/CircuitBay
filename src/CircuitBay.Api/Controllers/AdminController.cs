using CircuitBay.Application.Services;
using CircuitBay.Contracts.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBay.Api.Controllers;

[Authorize(Roles = "Admin,InventoryManager")]
public sealed class AdminController(CommerceService commerce) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardSnapshotDto> Dashboard(CancellationToken ct) => commerce.DashboardAsync(ct);

    [HttpGet("inventory")]
    public Task<IReadOnlyCollection<InventoryDto>> Inventory(CancellationToken ct) => commerce.InventoryAsync(ct);

    [HttpPost("inventory/receipts")]
    public Task<InventoryDto> Receive(InventoryReceiptRequest request, CancellationToken ct) => commerce.ReceiveInventoryAsync(request, ct);

    [Authorize(Roles = "Admin")]
    [HttpGet("coupons")]
    public Task<IReadOnlyCollection<CouponDto>> Coupons(CancellationToken ct) => commerce.CouponsAsync(ct);

    [Authorize(Roles = "Admin")]
    [HttpPost("coupons")]
    public Task<CouponDto> CreateCoupon(CouponRequest request, CancellationToken ct) => commerce.CreateCouponAsync(request, ct);

    [Authorize(Roles = "Admin")]
    [HttpGet("audit")]
    public Task<IReadOnlyCollection<AuditLogDto>> Audit([FromQuery] int take = 50, CancellationToken ct = default) => commerce.AuditLogsAsync(take, ct);
}
