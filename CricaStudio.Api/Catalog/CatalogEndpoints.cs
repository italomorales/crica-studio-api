using CricaStudio.Application.Catalog;
using CricaStudio.Domain.Catalog;

namespace CricaStudio.Api.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog").AddEndpointFilter<TranslationEndpointFilter>();

        group.MapGet("/themes", async (CatalogReadService catalog, CancellationToken ct) =>
            Results.Ok((await catalog.GetThemesAsync(true, ct)).Select(t => new { id = t.Id, name = t.Name, active = t.IsActive, productCount = t.ProductCount })));

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

        group.MapGet("/products", async (int page, int pageSize, string? query, Guid? typeId, bool featured, string? themes, CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            if (!TryParseThemeIds(themes, out var themeIds))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["themes"] = ["Selecione até 50 temas válidos."] });
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 24);
            var result = await catalog.GetPublishedShopProductPageAsync(page, pageSize, query, typeId, featured, cancellationToken, themeIds);
            return Results.Ok(new
            {
                items = result.Items.Select(product => new
                {
                    id = product.Id,
                    typeId = product.TypeId,
                    themeIds = product.ThemeIds,
                    name = product.Name,
                    slug = product.Slug,
                    description = product.Description,
                    fullDescription = product.FullDescription,
                    priceMode = product.PriceMode,
                    price = product.Price,
                    demo = product.IsDemo,
                    featured = product.IsFeatured,
                    characteristics = product.Characteristics,
                    personalization = product.Personalization,
                    images = product.Images.Select(image => image.Url),
                }),
                result.Total,
                page,
                pageSize,
            });
        });

        group.MapGet("/products/{slug}", async (string slug, CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            var product = await catalog.GetPublishedShopProductBySlugAsync(slug, cancellationToken);
            return product is null ? Results.NotFound() : Results.Ok(new
            {
                id = product.Id,
                typeId = product.TypeId,
                    themeIds = product.ThemeIds,
                name = product.Name,
                slug = product.Slug,
                description = product.Description,
                fullDescription = product.FullDescription,
                priceMode = product.PriceMode,
                price = product.Price,
                demo = product.IsDemo,
                featured = product.IsFeatured,
                characteristics = product.Characteristics,
                personalization = product.Personalization,
                images = product.Images.Select(image => image.Url),
            });
        });

        group.MapGet("/affiliates", async (int page, int pageSize, string? query, string? platform, bool featured, string? types, string? market, CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            if (!TryMarketScope(market, out var international))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["market"] = ["Selecione o catálogo brasileiro ou internacional."] });
            if (!TryParseThemeIds(types, out var typeIds))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["types"] = ["Selecione até 50 tipos válidos."] });
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 24);
            var result = await catalog.GetPublishedAffiliateProductPageAsync(page, pageSize, query, platform, featured, cancellationToken, typeIds, international);
            return Results.Ok(new
            {
                items = result.Items.Select(product => new
                {
                    id = product.Id,
                    typeId = product.TypeId,
                    name = product.Name,
                    description = product.Description,
                    platform = product.Platform, platformId = product.PlatformId,
                    international = product.IsInternational,
                    image = product.ImageUrl,
                    images = product.Images,
                    url = product.AffiliateUrl,
                    seller = product.Seller,
                    demoListing = product.IsDemoListing,
                    featured = product.IsFeatured,
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

    internal static bool TryMarketScope(string? market, out bool international)
    {
        international = market == "international";
        return market is null or "br" or "international";
    }

    internal static bool TryParseThemeIds(string? value, out Guid[] ids)
    {
        ids = [];
        if (string.IsNullOrWhiteSpace(value)) return true;
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length > 50 || parts.Any(part => !Guid.TryParse(part, out var id) || id == Guid.Empty)) return false;
        ids = parts.Select(Guid.Parse).Distinct().ToArray();
        return true;
    }
}
