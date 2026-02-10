using CircuitBay.Application.Services;
using CircuitBay.Contracts.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBay.Api.Controllers;

[Authorize]
public sealed class WishlistController(CommerceService commerce) : ApiControllerBase
{
    [HttpGet]
    public Task<WishlistDto> Get(CancellationToken ct) => commerce.WishlistAsync(CurrentUser, ct);

    [HttpPut("{productId:guid}")]
    public Task Add(Guid productId, CancellationToken ct) => commerce.AddWishlistAsync(CurrentUser, productId, ct);

    [HttpDelete("{productId:guid}")]
    public Task Remove(Guid productId, CancellationToken ct) => commerce.RemoveWishlistAsync(CurrentUser, productId, ct);
}
