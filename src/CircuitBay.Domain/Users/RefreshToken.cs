using CircuitBay.Domain.Common;

namespace CircuitBay.Domain.Users;

// Stores hashed refresh tokens. We hash so a stolen DB doesn't immediately compromise sessions.
// Rotation is enforced at the application layer — each use generates a new token and revokes the old.
public sealed class RefreshToken : BaseEntity
{
    private RefreshToken() { }

    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt, string? issuedIpAddress = null)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        IssuedAt = DateTimeOffset.UtcNow;
        IssuedIpAddress = issuedIpAddress;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string? IssuedIpAddress { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;
    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    public void Revoke(DateTimeOffset now, string? replacedBy = null)
    {
        if (IsRevoked) return;
        IsRevoked = true;
        RevokedAt = now;
        ReplacedByTokenHash = replacedBy;
        Touch();
    }
}
