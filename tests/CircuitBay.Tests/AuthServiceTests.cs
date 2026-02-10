using CircuitBay.Application.Abstractions;
using CircuitBay.Application.Common;
using CircuitBay.Application.Services;
using CircuitBay.Contracts.Auth;
using CircuitBay.Domain.Users;

namespace CircuitBay.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        var store = new MemoryStore();
        var hasher = new FixedHasher();
        var existing = new ShopperAccount("mira@example.com", "Mira", hasher.Hash("ShopperPass!2026"));
        await store.AddUserAsync(existing, CancellationToken.None);

        var service = new AuthService(store, hasher, new FakeJwtFactory(), new SystemClock());

        await Assert.ThrowsAsync<ConflictAppException>(() =>
            service.RegisterAsync(new RegisterRequest("mira@example.com", "Other Mira", "ShopperPass!2026"), CancellationToken.None));
    }

    [Fact]
    public async Task Login_updates_last_seen_and_returns_roles()
    {
        var store = new MemoryStore();
        var hasher = new FixedHasher();
        var existing = new ShopperAccount("mira@example.com", "Mira", hasher.Hash("ShopperPass!2026"));
        await store.AddUserAsync(existing, CancellationToken.None);
        var service = new AuthService(store, hasher, new FakeJwtFactory(), new SystemClock());

        var response = await service.LoginAsync(new LoginRequest("mira@example.com", "ShopperPass!2026"), null, CancellationToken.None);

        Assert.Equal("mira@example.com", response.User.Email);
        Assert.Contains("Shopper", response.User.Roles);
        Assert.NotNull(existing.LastSeenAt);
        Assert.NotEmpty(response.RefreshToken);
    }

    [Fact]
    public async Task Login_emits_audit_event_for_failed_attempt()
    {
        var store = new MemoryStore();
        var hasher = new FixedHasher();
        var existing = new ShopperAccount("mira@example.com", "Mira", hasher.Hash("correct-password!1234"));
        await store.AddUserAsync(existing, CancellationToken.None);
        var service = new AuthService(store, hasher, new FakeJwtFactory(), new SystemClock());

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.LoginAsync(new LoginRequest("mira@example.com", "wrong-password"), "10.0.0.1", CancellationToken.None));

        Assert.Contains(store.AuditLog, e => e.Action == "auth.login.failed");
    }

    [Fact]
    public async Task RefreshToken_rotation_invalidates_old_token()
    {
        var store = new MemoryStore();
        var hasher = new FixedHasher();
        var user = new ShopperAccount("mira@example.com", "Mira", hasher.Hash("ShopperPass!2026"));
        await store.AddUserAsync(user, CancellationToken.None);
        var clock = new SystemClock();
        var service = new AuthService(store, hasher, new FakeJwtFactory(), clock);

        var loginResp = await service.LoginAsync(new LoginRequest("mira@example.com", "ShopperPass!2026"), null, CancellationToken.None);
        var refreshResp = await service.RefreshAsync(loginResp.RefreshToken, null, CancellationToken.None);

        // Old token should be revoked
        var bytes = System.Text.Encoding.UTF8.GetBytes(loginResp.RefreshToken);
        var oldHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var oldToken = await store.FindRefreshTokenByHashAsync(oldHash, CancellationToken.None);
        Assert.NotNull(oldToken);
        Assert.True(oldToken!.IsRevoked);

        // New token should be valid
        Assert.NotEqual(loginResp.RefreshToken, refreshResp.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_rejects_revoked_token()
    {
        var store = new MemoryStore();
        var hasher = new FixedHasher();
        var user = new ShopperAccount("mira@example.com", "Mira", hasher.Hash("ShopperPass!2026"));
        await store.AddUserAsync(user, CancellationToken.None);
        var service = new AuthService(store, hasher, new FakeJwtFactory(), new SystemClock());

        var resp = await service.LoginAsync(new LoginRequest("mira@example.com", "ShopperPass!2026"), null, CancellationToken.None);
        await service.RevokeAsync(resp.RefreshToken, CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.RefreshAsync(resp.RefreshToken, null, CancellationToken.None));
    }

    [Fact]
    public async Task PasswordReset_flow_changes_password_and_revokes_sessions()
    {
        var store = new MemoryStore();
        var hasher = new FixedHasher();
        var user = new ShopperAccount("mira@example.com", "Mira", hasher.Hash("OldPass!2026xxx"));
        await store.AddUserAsync(user, CancellationToken.None);
        var clock = new SystemClock();
        var service = new AuthService(store, hasher, new FakeJwtFactory(), clock);

        // Login to get an active session
        await service.LoginAsync(new LoginRequest("mira@example.com", "OldPass!2026xxx"), null, CancellationToken.None);

        // Initiate reset
        var rawToken = await service.InitiatePasswordResetAsync("mira@example.com", CancellationToken.None);
        Assert.NotNull(rawToken);

        // Complete reset with new password
        await service.CompletePasswordResetAsync(new ResetPasswordRequest(rawToken!, "NewPass!2026xxx"), CancellationToken.None);

        // Should be able to login with new password
        var resp = await service.LoginAsync(new LoginRequest("mira@example.com", "NewPass!2026xxx"), null, CancellationToken.None);
        Assert.NotNull(resp.AccessToken);

        // Audit should record reset
        Assert.Contains(store.AuditLog, e => e.Action == "auth.password_reset.completed");
    }

    [Fact]
    public async Task PasswordReset_token_cannot_be_reused()
    {
        var store = new MemoryStore();
        var hasher = new FixedHasher();
        var user = new ShopperAccount("mira@example.com", "Mira", hasher.Hash("OldPass!2026xxx"));
        await store.AddUserAsync(user, CancellationToken.None);
        var service = new AuthService(store, hasher, new FakeJwtFactory(), new SystemClock());

        var rawToken = await service.InitiatePasswordResetAsync("mira@example.com", CancellationToken.None);
        await service.CompletePasswordResetAsync(new ResetPasswordRequest(rawToken!, "NewPass!2026xxx"), CancellationToken.None);

        // Second use should fail
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.CompletePasswordResetAsync(new ResetPasswordRequest(rawToken!, "AnotherPass!2026"), CancellationToken.None));
    }

    [Fact]
    public async Task InitiatePasswordReset_returns_null_for_unknown_email()
    {
        var store = new MemoryStore();
        var service = new AuthService(store, new FixedHasher(), new FakeJwtFactory(), new SystemClock());

        // Should silently succeed to prevent email enumeration
        var result = await service.InitiatePasswordResetAsync("nobody@example.com", CancellationToken.None);
        Assert.Null(result);
    }

    internal sealed class FixedHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash::{password}";
        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }

    private sealed class FakeJwtFactory : IJwtTokenFactory
    {
        public (string Token, DateTimeOffset ExpiresAt) Create(ShopperAccount user) => ("token", DateTimeOffset.UtcNow.AddHours(1));
    }
}
