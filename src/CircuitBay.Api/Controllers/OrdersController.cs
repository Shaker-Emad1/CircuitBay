using CircuitBay.Application.Services;
using CircuitBay.Contracts.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBay.Api.Controllers;

[Authorize]
public sealed class OrdersController(CommerceService commerce) : ApiControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<OrderDto>> Mine(CancellationToken ct) => commerce.OrdersAsync(CurrentUser, ct);
}
