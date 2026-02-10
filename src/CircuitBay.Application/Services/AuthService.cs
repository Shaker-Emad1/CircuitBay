using System.Security.Cryptography;
using CircuitBay.Application.Abstractions;
using CircuitBay.Application.Common;
using CircuitBay.Contracts.Auth;
using CircuitBay.Domain.Users;

namespace CircuitBay.Application.Services;

// Auth lifecycle: register/login issues both access+refresh tokens.
// Refresh rotates the token pair. Revoke invalidates a specific refresh token.
// Password reset is token-based via email (email dispatch is out of scope here — caller handles it).
//
// Login failures don't currently lock accounts after N attempts — that's tracked in the TODO.
// The IsLocked flag exists but we don't auto-lock. A deliberate choice to avoid self-DoS via lockout.
public sealed class AuthService(
    IStoreData data,
    IPasswordHasher passwordHasher,
    IJwtTokenFactory tokenFactory,
    IClock clock)
{
    // 30 day refresh window. Could be configurable but hardcoding is fine for now.
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ValidationAppException("A valid email address is required.");
        if (request.Password.Length < 10)
            throw new ValidationAppException("Password must be at least 10 characters.");
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new ValidationAppException("Display name is required.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await data.FindUserByEmailAsync(normalizedEmail, ct) is not null)
            throw new ConflictAppException("An account already exists for that email.");

        var user = new ShopperAccount(normalizedEmail, request.DisplayName, passwordHasher.Hash(request.Password));
        await data.AddUserAsync(user, ct);
        await data.SaveChangesAsync(ct);

        var (accessToken, expiresAt) = tokenFactory.Create(user);
        var (rawRefresh, refreshToken) = CreateRefreshToken(user.Id);
        await data.AddRefreshTokenAsync(refreshToken, ct);
        await data.AddAuditAsync(user.Id, "auth.register", nameof(ShopperAccount), user.Id, $"Account created for {user.Email}.", ct);
        await data.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, expiresAt, rawRefresh, MapProfile(user));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct)
    {
        var user = await data.FindUserByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct)
            ?? throw new UnauthorizedAppException("Invalid email or password.");

        if (user.IsLocked || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await data.AddAuditAsync(user.Id, "auth.login.failed", nameof(ShopperAccount), user.Id,
                $"Failed login attempt from {ipAddress ?? "unknown"}.", ct);
            await data.SaveChangesAsync(ct);
            throw new UnauthorizedAppException("Invalid email or password.");
        }

        user.MarkSeen();
        var (accessToken, expiresAt) = tokenFactory.Create(user);
        var (rawRefresh, refreshToken) = CreateRefreshToken(user.Id, ipAddress);
        await data.AddRefreshTokenAsync(refreshToken, ct);
        await data.AddAuditAsync(user.Id, "auth.login", nameof(ShopperAccount), user.Id,
            $"Login from {ipAddress ?? "unknown"}.", ct);
        await data.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, expiresAt, rawRefresh, MapProfile(user));
    }

    public async Task<AuthResponse> RefreshAsync(string rawToken, string? ipAddress, CancellationToken ct)
    {
        var hash = HashToken(rawToken);
        var stored = await data.FindRefreshTokenByHashAsync(hash, ct)
            ?? throw new UnauthorizedAppException("Refresh token not recognized.");

        if (!stored.IsActive(clock.UtcNow))
            throw new UnauthorizedAppException("Refresh token has expired or been revoked.");

        var user = await data.FindUserAsync(stored.UserId, ct)
            ?? throw new UnauthorizedAppException("Associated account no longer exists.");

        if (user.IsLocked)
            throw new UnauthorizedAppException("Account has been locked.");

        // Rotate: revoke old, issue new
        var (newRaw, newToken) = CreateRefreshToken(user.Id, ipAddress);
        stored.Revoke(clock.UtcNow, newToken.TokenHash);
        await data.AddRefreshTokenAsync(newToken, ct);

        var (accessToken, expiresAt) = tokenFactory.Create(user);
        await data.AddAuditAsync(user.Id, "auth.token.refreshed", nameof(ShopperAccount), user.Id, "Token pair rotated.", ct);
        await data.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, expiresAt, newRaw, MapProfile(user));
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct)
    {
        var hash = HashToken(rawToken);
        var stored = await data.FindRefreshTokenByHashAsync(hash, ct);
        if (stored is null || !stored.IsActive(clock.UtcNow)) return; // silently ignore invalid/expired tokens

        stored.Revoke(clock.UtcNow);
        await data.AddAuditAsync(stored.UserId, "auth.token.revoked", nameof(RefreshToken), stored.Id, "Token explicitly revoked.", ct);
        await data.SaveChangesAsync(ct);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        await data.RevokeAllUserRefreshTokensAsync(userId, clock.UtcNow, ct);
        await data.AddAuditAsync(userId, "auth.tokens.revoked_all", nameof(ShopperAccount), userId, "All sessions terminated.", ct);
        await data.SaveChangesAsync(ct);
    }

    // Generates a password reset token and returns the raw token (caller is responsible for email dispatch).
    // We intentionally do NOT confirm whether the email exists to prevent user enumeration.
    public async Task<string?> InitiatePasswordResetAsync(string email, CancellationToken ct)
    {
        var user = await data.FindUserByEmailAsync(email.Trim().ToLowerInvariant(), ct);
        if (user is null) return null; // caller should respond identically regardless

        await data.InvalidateUserPasswordResetTokensAsync(user.Id, ct);

        var rawToken = GenerateSecureToken();
        var hash = HashToken(rawToken);
        var resetToken = new PasswordResetToken(user.Id, hash, clock.UtcNow.AddHours(2));
        await data.AddPasswordResetTokenAsync(resetToken, ct);
        await data.AddAuditAsync(user.Id, "auth.password_reset.requested", nameof(ShopperAccount), user.Id, "Reset token issued.", ct);
        await data.SaveChangesAsync(ct);

        return rawToken;
    }

    public async Task CompletePasswordResetAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword.Length < 10)
            throw new ValidationAppException("Password must be at least 10 characters.");

        var hash = HashToken(request.Token);
        var resetToken = await data.FindPasswordResetTokenByHashAsync(hash, ct)
            ?? throw new ValidationAppException("Reset token is invalid or has already been used.");

        if (!resetToken.IsValid(clock.UtcNow))
            throw new ValidationAppException("Reset token has expired.");

        var user = await data.FindUserAsync(resetToken.UserId, ct)
            ?? throw new ValidationAppException("Associated account no longer exists.");

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));
        resetToken.MarkUsed(clock.UtcNow);

        // Invalidate all active sessions on password change
        await data.RevokeAllUserRefreshTokensAsync(user.Id, clock.UtcNow, ct);
        await data.AddAuditAsync(user.Id, "auth.password_reset.completed", nameof(ShopperAccount), user.Id, "Password changed via reset.", ct);
        await data.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword.Length < 10)
            throw new ValidationAppException("Password must be at least 10 characters.");

        var user = await data.FindUserAsync(userId, ct) ?? throw new UnauthorizedAppException("User not found.");
        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationAppException("Current password is incorrect.");

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));
        await data.RevokeAllUserRefreshTokensAsync(userId, clock.UtcNow, ct);
        await data.AddAuditAsync(userId, "auth.password.changed", nameof(ShopperAccount), userId, "Password changed by user.", ct);
        await data.SaveChangesAsync(ct);
    }

    private (string Raw, RefreshToken Token) CreateRefreshToken(Guid userId, string? ipAddress = null)
    {
        var raw = GenerateSecureToken();
        var hash = HashToken(raw);
        var token = new RefreshToken(userId, hash, clock.UtcNow.Add(RefreshTokenLifetime), ipAddress);
        return (raw, token);
    }

    private static string GenerateSecureToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace("+", "-").Replace("/", "_").TrimEnd('=');

    internal static string HashToken(string raw)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static UserProfileDto MapProfile(ShopperAccount user) =>
        new(user.Id, user.Email, user.DisplayName, user.Roles.Select(x => x.ToString()).ToArray());
}
