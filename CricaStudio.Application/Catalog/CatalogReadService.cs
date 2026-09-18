using CricaStudio.Domain.Catalog;

namespace CricaStudio.Application.Catalog;

public sealed class CatalogReadService(ICatalogReadRepository repository)
{
    public Task<IReadOnlyList<CatalogType>> GetPublishedTypesAsync(CancellationToken cancellationToken) =>
        repository.GetPublishedTypesAsync(cancellationToken);

    public Task<IReadOnlyList<CatalogType>> GetAllTypesAsync(CancellationToken cancellationToken) =>
        repository.GetAllTypesAsync(cancellationToken);

    public Task<IReadOnlyList<ShopProduct>> GetPublishedShopProductsAsync(CancellationToken cancellationToken) =>
        repository.GetPublishedShopProductsAsync(cancellationToken);

    public Task<CatalogPage<ShopProduct>> GetPublishedShopProductPageAsync(int page, int pageSize, string? query, Guid? typeId, CancellationToken cancellationToken) =>
        repository.GetPublishedShopProductPageAsync(page, pageSize, query, typeId, cancellationToken);

    public Task<IReadOnlyList<ShopProduct>> GetAllShopProductsAsync(CancellationToken cancellationToken) =>
        repository.GetAllShopProductsAsync(cancellationToken);

    public Task<IReadOnlyList<AffiliateProduct>> GetPublishedAffiliateProductsAsync(CancellationToken cancellationToken) =>
        repository.GetPublishedAffiliateProductsAsync(cancellationToken);

    public Task<CatalogPage<AffiliateProduct>> GetPublishedAffiliateProductPageAsync(int page, int pageSize, string? query, string? platform, CancellationToken cancellationToken) =>
        repository.GetPublishedAffiliateProductPageAsync(page, pageSize, query, platform, cancellationToken);

    public Task<IReadOnlyList<AffiliateProduct>> GetAllAffiliateProductsAsync(CancellationToken cancellationToken) =>
        repository.GetAllAffiliateProductsAsync(cancellationToken);

    public Task<CatalogSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        repository.GetSettingsAsync(cancellationToken);
}
