using CircuitBay.Application.Services;
using CircuitBay.Contracts.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBay.Api.Controllers;

[Authorize]
public sealed class CartController(CommerceService commerce) : ApiControllerBase
{
    [HttpGet]
    public Task<CartDto> Get(CancellationToken ct) => commerce.GetCartAsync(CurrentUser, ct);

    [HttpPut("lines")]
    public Task<CartDto> PutLine(CartLineRequest request, CancellationToken ct) => commerce.PutCartLineAsync(CurrentUser, request, ct);

    [HttpDelete("lines/{productId:guid}")]
    public Task RemoveLine(Guid productId, CancellationToken ct) => commerce.RemoveCartLineAsync(CurrentUser, productId, ct);

    [HttpPut("coupon")]
    public Task<CartDto> ApplyCoupon(ApplyCouponRequest request, CancellationToken ct) => commerce.ApplyCouponAsync(CurrentUser, request, ct);

    [HttpDelete("coupon")]
    public Task<CartDto> RemoveCoupon(CancellationToken ct) => commerce.RemoveCouponAsync(CurrentUser, ct);
}
