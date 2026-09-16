namespace CricaStudio.Domain.Catalog;

public interface ICatalogWriteRepository
{
    Task<CatalogType> SaveTypeAsync(CatalogType type, CancellationToken cancellationToken);
    Task<ShopProduct> SaveShopProductAsync(ShopProduct product, CancellationToken cancellationToken);
    Task<AffiliateProduct> SaveAffiliateProductAsync(AffiliateProduct product, CancellationToken cancellationToken);
    Task SaveSettingsAsync(CatalogSettings settings, CancellationToken cancellationToken);
    Task DeleteTypeAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteShopProductAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAffiliateProductAsync(Guid id, CancellationToken cancellationToken);
}
