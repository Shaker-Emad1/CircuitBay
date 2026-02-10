using System.Security.Claims;
using CircuitBay.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected CurrentUser CurrentUser
    {
        get
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            if (!Guid.TryParse(id, out var userId))
                throw new UnauthorizedAppException("Authenticated user id is missing.");

            return new CurrentUser(userId, email, User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray());
        }
    }
}
