namespace CricaStudio.Domain.Catalog;

public sealed record CatalogPage<T>(IReadOnlyList<T> Items, int Total);

public sealed record CatalogType(Guid Id, string Name, string Scope, bool IsActive);

public sealed record ProductImage(Guid Id, string Url, int SortOrder);

public sealed record ShopProduct(
    Guid Id,
    Guid TypeId,
    string Name,
    string Description,
    string? FullDescription,
    string PriceMode,
    decimal? Price,
    bool IsDemo,
    IReadOnlyList<string> Characteristics,
    IReadOnlyList<string> Personalization,
    IReadOnlyList<ProductImage> Images,
    string Status,
    int SortOrder);

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
    string Status,
    int SortOrder);

public sealed record CatalogSettings(string WhatsappNumber);
