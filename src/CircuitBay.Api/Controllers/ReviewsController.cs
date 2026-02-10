using CircuitBay.Application.Services;
using CircuitBay.Contracts.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBay.Api.Controllers;

[Authorize]
public sealed class ReviewsController(CommerceService commerce) : ApiControllerBase
{
    [HttpPost]
    public Task<ReviewDto> Create(ReviewRequest request, CancellationToken ct) => commerce.ReviewAsync(CurrentUser, request, ct);
}
