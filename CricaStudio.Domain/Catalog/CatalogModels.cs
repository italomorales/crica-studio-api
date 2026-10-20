namespace CricaStudio.Domain.Catalog;

public sealed record CatalogPage<T>(IReadOnlyList<T> Items, int Total);

public sealed record CatalogType(Guid Id, string Name, string Scope, bool IsActive) : ITranslationInput
{ public Dictionary<string,CatalogTranslation>? Translations { get; init; } }

public sealed record CatalogTheme(Guid Id, string Name, bool IsActive, int ProductCount = 0) : ITranslationInput
{ public Dictionary<string,CatalogTranslation>? Translations { get; init; } }
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
    int SortOrder) : ITranslationInput
{
    public SupplierManagement Management { get; init; } = new(null);
    public Dictionary<string,CatalogTranslation>? Translations { get; init; }
    public IReadOnlyList<string> Images { get; init; } = [];
    public bool IsInternational { get; init; }
    public Guid? PlatformId { get; init; }
}

public sealed record SupplierManagement(decimal? LastStorePrice, string LinkStatus = "unchecked", string StockStatus = "unchecked")
{
    public bool IsValid() => (LastStorePrice is null || (LastStorePrice >= 0 && LastStorePrice <= 9999999999.99m && decimal.Round(LastStorePrice.Value,2) == LastStorePrice))
        && LinkStatus is "unchecked" or "ok" or "broken"
        && StockStatus is "unchecked" or "in_stock" or "out_of_stock";
}

public sealed record CatalogSettings(string WhatsappNumber);
