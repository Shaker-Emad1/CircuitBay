namespace CircuitBay.Contracts.Common;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize));
}

public sealed record ApiError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Details = null)
{
    public string? CorrelationId { get; init; }
}

