using CircuitBay.Domain.Users;

namespace CircuitBay.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IJwtTokenFactory
{
    (string Token, DateTimeOffset ExpiresAt) Create(ShopperAccount user);
}
