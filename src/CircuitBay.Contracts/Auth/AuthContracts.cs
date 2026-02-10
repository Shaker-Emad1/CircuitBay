namespace CircuitBay.Contracts.Auth;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, UserProfileDto User);
public sealed record UserProfileDto(Guid Id, string Email, string DisplayName, IReadOnlyCollection<string> Roles);
public sealed record RefreshRequest(string RefreshToken);
public sealed record RevokeTokenRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

