using CricaStudio.Application.Catalog;

namespace CricaStudio.Api.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        group.MapGet("/types", async (CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            var types = await catalog.GetPublishedTypesAsync(cancellationToken);
            return Results.Ok(types.Select(type => new
            {
                id = type.Id,
                name = type.Name,
                scope = type.Scope,
                active = type.IsActive,
            }));
        });

        group.MapGet("/products", async (int page, int pageSize, string? query, Guid? typeId, CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 24);
            var result = await catalog.GetPublishedShopProductPageAsync(page, pageSize, query, typeId, cancellationToken);
            return Results.Ok(new
            {
                items = result.Items.Select(product => new
                {
                    id = product.Id,
                    typeId = product.TypeId,
                    name = product.Name,
                    description = product.Description,
                    fullDescription = product.FullDescription,
                    priceMode = product.PriceMode,
                    price = product.Price,
                    demo = product.IsDemo,
                    characteristics = product.Characteristics,
                    personalization = product.Personalization,
                    images = product.Images.Select(image => image.Url),
                }),
                result.Total,
                page,
                pageSize,
            });
        });

        group.MapGet("/affiliates", async (int page, int pageSize, string? query, string? platform, CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 24);
            var result = await catalog.GetPublishedAffiliateProductPageAsync(page, pageSize, query, platform, cancellationToken);
            return Results.Ok(new
            {
                items = result.Items.Select(product => new
                {
                    id = product.Id,
                    typeId = product.TypeId,
                    name = product.Name,
                    description = product.Description,
                    platform = product.Platform,
                    image = product.ImageUrl,
                    url = product.AffiliateUrl,
                    seller = product.Seller,
                    demoListing = product.IsDemoListing,
                }),
                result.Total,
                page,
                pageSize,
            });
        });

        group.MapGet("/settings", async (CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            var settings = await catalog.GetSettingsAsync(cancellationToken);
            return Results.Ok(new { whatsappNumber = settings.WhatsappNumber });
        });
    }
}
