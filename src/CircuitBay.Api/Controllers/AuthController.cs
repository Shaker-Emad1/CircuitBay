using CircuitBay.Application.Common;
using CircuitBay.Application.Services;
using CircuitBay.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBay.Api.Controllers;

public sealed class AuthController(AuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) =>
        auth.RegisterAsync(request, ct);

    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return auth.LoginAsync(request, ip, ct);
    }

    [HttpPost("refresh")]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return auth.RefreshAsync(request.RefreshToken, ip, ct);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RevokeTokenRequest request, CancellationToken ct)
    {
        await auth.RevokeAsync(request.RefreshToken, ct);
        return Ok();
    }

    // No email gateway wired up yet — returns the raw token in the response body for dev/test.
    // In production this would dispatch an email and return 204.
    // TODO: integrate SendGrid/Postmark when email infra is added.
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        var token = await auth.InitiatePasswordResetAsync(request.Email, ct);
#if DEBUG
        // Expose token in dev so integration tests and local testing don't need email
        return token is null ? Ok(new { message = "If that email exists, a reset link was sent." })
            : Ok(new { message = "Reset token issued.", token });
#else
        return Ok(new { message = "If that email exists, a reset link was sent." });
#endif
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.CompletePasswordResetAsync(request, ct);
        return Ok(new { message = "Password updated. All existing sessions have been invalidated." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(CurrentUser.Id, request, ct);
        return Ok(new { message = "Password changed. Please log in again." });
    }

    [Authorize]
    [HttpPost("revoke-all")]
    public async Task<IActionResult> RevokeAll(CancellationToken ct)
    {
        await auth.RevokeAllAsync(CurrentUser.Id, ct);
        return Ok(new { message = "All sessions terminated." });
    }
}
