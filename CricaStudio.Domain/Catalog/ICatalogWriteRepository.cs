namespace CricaStudio.Domain.Catalog;

public interface ICatalogWriteRepository
{
    Task<bool> ReorderAsync(bool suppliers, IReadOnlyList<Guid> ids, IReadOnlyList<CatalogOrderEntry> expected, CancellationToken cancellationToken);
    Task<CatalogType> SaveTypeAsync(CatalogType type, CancellationToken cancellationToken);
    Task<ShopProduct> SaveShopProductAsync(ShopProduct product, CancellationToken cancellationToken);
    Task<int> CountPublishedFeaturedShopProductsAsync(Guid excludingId, CancellationToken cancellationToken);
    Task<AffiliateProduct> SaveAffiliateProductAsync(AffiliateProduct product, CancellationToken cancellationToken);
    Task<int> CountPublishedFeaturedAffiliateProductsAsync(Guid excludingId, CancellationToken cancellationToken);
    Task SaveSettingsAsync(CatalogSettings settings, CancellationToken cancellationToken);
    Task DeleteTypeAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteShopProductAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAffiliateProductAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record CatalogOrderEntry(Guid Id, int Order);
