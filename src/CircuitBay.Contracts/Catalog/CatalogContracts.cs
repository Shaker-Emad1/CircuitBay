using CircuitBay.Contracts.Common;

namespace CircuitBay.Contracts.Catalog;

public sealed record ProductSearchRequest(
    string? Query,
    Guid? CategoryId,
    string? Brand,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool InStockOnly = false,
    string Sort = "relevance",
    int Page = 1,
    int PageSize = 20);

public sealed record ProductSummaryDto(
    Guid Id,
    string Sku,
    string Name,
    string Brand,
    string Slug,
    string Category,
    decimal Price,
    string Currency,
    int Sellable,
    decimal AverageRating,
    int ReviewCount);

public sealed record ProductDetailDto(
    Guid Id,
    string Sku,
    string Name,
    string Brand,
    string Slug,
    string Description,
    string Category,
    decimal Price,
    string Currency,
    int Sellable,
    decimal AverageRating,
    int ReviewCount,
    IReadOnlyDictionary<string, string> Specifications);

public sealed record UpsertProductRequest(
    string Sku,
    string Name,
    string Brand,
    string Slug,
    Guid CategoryId,
    decimal Price,
    string Currency,
    string Description,
    IReadOnlyDictionary<string, string> Specifications);

public sealed record CategoryDto(Guid Id, string Name, string Slug, Guid? ParentCategoryId);
public sealed record CategoryRequest(string Name, string Slug, Guid? ParentCategoryId);
public sealed record SearchEnvelope(PagedResult<ProductSummaryDto> Results, IReadOnlyCollection<string> Facets);
