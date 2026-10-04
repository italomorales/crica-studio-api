namespace CricaStudio.Domain.Catalog;

public sealed record CatalogPage<T>(IReadOnlyList<T> Items, int Total);

public sealed record CatalogType(Guid Id, string Name, string Scope, bool IsActive);

public sealed record CatalogTheme(Guid Id, string Name, bool IsActive, int ProductCount = 0);
public sealed class CatalogThemeValidationException(string message) : Exception(message);

public sealed record ProductImage(Guid Id, string Url, int SortOrder);

public sealed record ShopProduct(
    Guid Id,
    Guid TypeId,
    string Name,
    string Slug,
    string Description,
    string? FullDescription,
    string PriceMode,
    decimal? Price,
    bool IsDemo,
    bool IsFeatured,
    IReadOnlyList<string> Characteristics,
    IReadOnlyList<string> Personalization,
    IReadOnlyList<ProductImage> Images,
    string Status,
    int SortOrder)
{
    public IReadOnlyList<Guid> ThemeIds { get; init; } = [];
}

public sealed record AffiliateProduct(
    Guid Id,
    Guid TypeId,
    string Name,
    string Description,
    string Platform,
    string? ImageUrl,
    string? AffiliateUrl,
    string? Seller,
    bool IsDemoListing,
    bool IsFeatured,
    string Status,
    int SortOrder)
{
    public IReadOnlyList<string> Images { get; init; } = [];
    public bool IsInternational { get; init; }
    public Guid? PlatformId { get; init; }
}

public sealed record CatalogSettings(string WhatsappNumber);
