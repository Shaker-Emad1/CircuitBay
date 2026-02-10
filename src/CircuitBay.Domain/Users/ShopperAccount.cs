using CircuitBay.Domain.Common;
using CircuitBay.Domain.Enums;

namespace CircuitBay.Domain.Users;

public sealed class ShopperAccount : BaseEntity
{
    private ShopperAccount() { }

    public ShopperAccount(string email, string displayName, string passwordHash)
    {
        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        PasswordHash = passwordHash;
        Roles.Add(UserRole.Shopper);
    }

    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsLocked { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }
    public ICollection<UserRole> Roles { get; private set; } = new List<UserRole>();

    public void AddRole(UserRole role)
    {
        if (!Roles.Contains(role))
            Roles.Add(role);
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash)) throw new InvalidOperationException("Password hash cannot be empty.");
        PasswordHash = newPasswordHash;
        Touch();
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new InvalidOperationException("Display name cannot be empty.");
        DisplayName = displayName.Trim();
        Touch();
    }

    public void MarkSeen() => LastSeenAt = DateTimeOffset.UtcNow;
    public void Lock() => IsLocked = true;
    public void Unlock() => IsLocked = false;
}
