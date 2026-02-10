using CircuitBay.Application.Services;
using CircuitBay.Contracts.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBay.Api.Controllers;

public sealed class CatalogController(CatalogService catalog) : ApiControllerBase
{
    [HttpGet("products")]
    public Task<SearchEnvelope> Search([FromQuery] ProductSearchRequest request, CancellationToken ct) => catalog.SearchAsync(request, ct);

    [HttpGet("products/{id:guid}")]
    public Task<ProductDetailDto> Get(Guid id, CancellationToken ct) => catalog.GetAsync(id, ct);

    [HttpGet("categories")]
    public Task<IReadOnlyCollection<CategoryDto>> Categories(CancellationToken ct) => catalog.CategoriesAsync(ct);

    [Authorize(Roles = "Admin,InventoryManager")]
    [HttpPost("products")]
    public Task<ProductDetailDto> CreateProduct(UpsertProductRequest request, CancellationToken ct) => catalog.CreateAsync(request, ct);

    [Authorize(Roles = "Admin")]
    [HttpPost("categories")]
    public Task<CategoryDto> CreateCategory(CategoryRequest request, CancellationToken ct) => catalog.CreateCategoryAsync(request, ct);
}
