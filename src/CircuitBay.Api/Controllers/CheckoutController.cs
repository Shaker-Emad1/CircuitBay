using CircuitBay.Application.Services;
using CircuitBay.Contracts.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBay.Api.Controllers;

[Authorize]
public sealed class CheckoutController(CommerceService commerce) : ApiControllerBase
{
    [HttpPost]
    public Task<OrderDto> Checkout(CheckoutRequest request, CancellationToken ct) => commerce.CheckoutAsync(CurrentUser, request, ct);
}
