namespace CricaStudio.Domain.Catalog;

public sealed record CatalogPlatform(Guid Id, string Name, string Code, string Description, string? Url,
    string? LogoUrl, string Locale, string CountryCode, string Status, int Order, bool MobileOnly, bool Active)
{
    public int ProductCount { get; init; }
}
public sealed class PlatformValidationException(string message) : Exception(message);
public interface IPlatformRepository
{
    Task<IReadOnlyList<CatalogPlatform>> GetAsync(string? locale, string? country, bool publicOnly, bool storefrontsOnly, CancellationToken ct);
    Task<CatalogPlatform?> SaveAsync(CatalogPlatform platform, bool create, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
