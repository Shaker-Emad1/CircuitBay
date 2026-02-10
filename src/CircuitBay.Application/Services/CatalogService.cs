using CircuitBay.Application.Abstractions;
using CircuitBay.Application.Common;
using CircuitBay.Contracts.Catalog;
using CircuitBay.Domain.Catalog;

namespace CircuitBay.Application.Services;

public sealed class CatalogService(IStoreData data, IResponseCache? cache = null)
{
    public async Task<SearchEnvelope> SearchAsync(ProductSearchRequest request, CancellationToken ct)
    {
        var normalized = request with
        {
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 80)
        };
        var key = $"catalog:search:{await CacheVersionAsync(ct)}:{normalized.Query}:{normalized.CategoryId}:{normalized.Brand}:{normalized.MinPrice}:{normalized.MaxPrice}:{normalized.InStockOnly}:{normalized.Sort}:{normalized.Page}:{normalized.PageSize}";
        return await FromCacheAsync(key, () => SearchInternalAsync(normalized, ct), TimeSpan.FromMinutes(3), ct);
    }

    public async Task<ProductDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        await FromCacheAsync($"catalog:product:{await CacheVersionAsync(ct)}:{id}", async () =>
            await data.GetProductDetailAsync(id, ct) ?? throw new NotFoundAppException("Product was not found."),
            TimeSpan.FromMinutes(10),
            ct);

    public async Task<ProductDetailDto> CreateAsync(UpsertProductRequest request, CancellationToken ct)
    {
        ValidateProduct(request);
        if (await data.FindProductBySkuAsync(request.Sku, ct) is not null)
            throw new ConflictAppException("SKU is already assigned to another product.");
        if (await data.FindCategoryAsync(request.CategoryId, ct) is null)
            throw new ValidationAppException("Category does not exist.");

        var product = new Product(request.Sku, request.Name, request.Brand, request.Slug, request.CategoryId, request.Price, request.Currency);
        product.ReviseDetails(request.Description, request.Price);
        foreach (var pair in request.Specifications.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
            product.AddSpecification(pair.Key, pair.Value);
        product.Publish();

        await data.AddProductAsync(product, ct);
        await data.SaveChangesAsync(ct);
        if (cache is not null) await cache.BumpVersionAsync("catalog", ct);
        return await GetAsync(product.Id, ct);
    }

    public async Task<IReadOnlyCollection<CategoryDto>> CategoriesAsync(CancellationToken ct) =>
        await FromCacheAsync($"catalog:categories:{await CacheVersionAsync(ct)}", async () =>
            (await data.ListCategoriesAsync(ct)).Select(x => new CategoryDto(x.Id, x.Name, x.Slug, x.ParentCategoryId)).ToArray(),
            TimeSpan.FromMinutes(15),
            ct);

    public async Task<CategoryDto> CreateCategoryAsync(CategoryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
            throw new ValidationAppException("Category name and slug are required.");

        var category = new Category(request.Name, request.Slug, request.ParentCategoryId);
        await data.AddCategoryAsync(category, ct);
        await data.SaveChangesAsync(ct);
        if (cache is not null) await cache.BumpVersionAsync("catalog", ct);
        return new CategoryDto(category.Id, category.Name, category.Slug, category.ParentCategoryId);
    }

    private async Task<SearchEnvelope> SearchInternalAsync(ProductSearchRequest request, CancellationToken ct)
    {
        var page = await data.SearchProductsAsync(request, ct);
        var facets = page.Items.Select(x => x.Brand).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
        return new SearchEnvelope(page, facets);
    }

    private static void ValidateProduct(UpsertProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationAppException("SKU and product name are required.");
        if (request.Price <= 0)
            throw new ValidationAppException("Product price must be positive.");
        if (request.Currency.Length != 3)
            throw new ValidationAppException("Currency must be a three-letter code.");
    }

    private async Task<string> CacheVersionAsync(CancellationToken ct) =>
        cache is null ? "local" : await cache.VersionAsync("catalog", ct);

    private async Task<T> FromCacheAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken ct)
    {
        if (cache is not null)
        {
            var cached = await cache.GetAsync<T>(key, ct);
            if (cached is not null) return cached;
        }

        var value = await factory();
        if (cache is not null)
            await cache.SetAsync(key, value, ttl, ct);
        return value;
    }
}
