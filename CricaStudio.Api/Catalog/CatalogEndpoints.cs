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

        group.MapGet("/products", async (CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            var products = await catalog.GetPublishedShopProductsAsync(cancellationToken);
            return Results.Ok(products.Select(product => new
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
            }));
        });

        group.MapGet("/affiliates", async (CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            var products = await catalog.GetPublishedAffiliateProductsAsync(cancellationToken);
            return Results.Ok(products.Select(product => new
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
            }));
        });

        group.MapGet("/settings", async (CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            var settings = await catalog.GetSettingsAsync(cancellationToken);
            return Results.Ok(new { whatsappNumber = settings.WhatsappNumber });
        });
    }
}
