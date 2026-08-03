using CircuitBay.Domain.Common;

namespace CircuitBay.Domain.Users;

// Single-use password reset tokens. Stored hashed; token is ephemeral (not logged).
// Intentionally short-lived — 2 hour window before the link expires.
public sealed class PasswordResetToken : BaseEntity
{
    private PasswordResetToken() { }

    public PasswordResetToken(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    public bool IsValid(DateTimeOffset now) => !IsUsed && ExpiresAt > now;

    public void MarkUsed(DateTimeOffset now)
    {
        IsUsed = true;
        UsedAt = now;
        Touch();
    }
}
