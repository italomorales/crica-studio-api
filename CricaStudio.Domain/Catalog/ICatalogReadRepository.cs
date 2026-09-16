namespace CricaStudio.Domain.Catalog;

public interface ICatalogReadRepository
{
    Task<IReadOnlyList<CatalogType>> GetPublishedTypesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogType>> GetAllTypesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ShopProduct>> GetPublishedShopProductsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ShopProduct>> GetAllShopProductsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AffiliateProduct>> GetPublishedAffiliateProductsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AffiliateProduct>> GetAllAffiliateProductsAsync(CancellationToken cancellationToken);
    Task<CatalogSettings> GetSettingsAsync(CancellationToken cancellationToken);
}
