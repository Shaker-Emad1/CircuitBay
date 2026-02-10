using CircuitBay.Domain.Common;

namespace CircuitBay.Domain.Catalog;

public sealed class Category : BaseEntity
{
    private Category() { }

    public Category(string name, string slug, Guid? parentCategoryId = null)
    {
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        ParentCategoryId = parentCategoryId;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Guid? ParentCategoryId { get; private set; }
    public Category? ParentCategory { get; private set; }
}

public sealed class Product : BaseEntity
{
    private readonly List<ProductSpecification> _specifications = [];

    private Product() { }

    public Product(string sku, string name, string brand, string slug, Guid categoryId, decimal price, string currency)
    {
        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        Brand = brand.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        CategoryId = categoryId;
        Price = price;
        Currency = currency;
    }

    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Brand { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "USD";
    public bool IsPublished { get; private set; }
    public decimal AverageRating { get; private set; }
    public int ReviewCount { get; private set; }
    public IReadOnlyCollection<ProductSpecification> Specifications => _specifications;

    public void Publish()
    {
        if (Price <= 0)
            throw new InvalidOperationException("Products need a positive price before publishing.");

        IsPublished = true;
        Touch();
    }

    public void ReviseDetails(string description, decimal price)
    {
        if (price <= 0)
            throw new InvalidOperationException("Product price must be positive.");

        Description = description.Trim();
        Price = price;
        Touch();
    }

    public void AddSpecification(string name, string value)
    {
        _specifications.RemoveAll(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        _specifications.Add(new ProductSpecification(Id, name, value));
        Touch();
    }

    public void RecalculateRating(IEnumerable<int> ratings)
    {
        var values = ratings.ToArray();
        ReviewCount = values.Length;
        AverageRating = values.Length == 0 ? 0 : Math.Round((decimal)values.Average(), 2);
        Touch();
    }
}

public sealed class ProductSpecification : BaseEntity
{
    private ProductSpecification() { }

    public ProductSpecification(Guid productId, string name, string value)
    {
        ProductId = productId;
        Name = name.Trim();
        Value = value.Trim();
    }

    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
}
