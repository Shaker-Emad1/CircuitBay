namespace CircuitBay.Application.Common;

public sealed record CurrentUser(Guid Id, string Email, IReadOnlyCollection<string> Roles);
