using CricaStudio.Application.Catalog;
using CricaStudio.Domain.Catalog;
using Amazon.S3;
using Microsoft.AspNetCore.Authorization;

namespace CricaStudio.Api.Catalog;

public static class AdminCatalogEndpoints
{
    public static void MapAdminCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/catalog").RequireAuthorization().WithTags("Admin catalog").AddEndpointFilter<TranslationEndpointFilter>();

        group.MapGet("/themes", async (CatalogReadService catalog, CancellationToken ct) =>
            Results.Ok((await catalog.GetThemesAsync(false, ct)).Select(t => new { id = t.Id, name = t.Name, active = t.IsActive, productCount = t.ProductCount })));
        group.MapPost("/themes", (ThemeRequest request, ICatalogWriteRepository repo, CancellationToken ct) => SaveTheme(request, Guid.Empty, repo, ct));
        group.MapPut("/themes/{id:guid}", (Guid id, ThemeRequest request, ICatalogWriteRepository repo, CancellationToken ct) => SaveTheme(request, id, repo, ct));
        group.MapDelete("/themes/{id:guid}", async (Guid id, ICatalogWriteRepository repo, CancellationToken ct) =>
        {
            try { await repo.DeleteThemeAsync(id, ct); return Results.NoContent(); }
            catch (CatalogThemeValidationException ex) { return ThemeProblem(ex.Message); }
        });

        group.MapGet("/types", async (CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            var types = await catalog.GetAllTypesAsync(cancellationToken);
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
            var products = await catalog.GetAllShopProductsAsync(cancellationToken);
            return Results.Ok(products.Select(product => new
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
                status = product.Status,
                order = product.SortOrder,
            }));
        });

        group.MapGet("/affiliates", async (CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            var products = await catalog.GetAllAffiliateProductsAsync(cancellationToken);
            return Results.Ok(products.Select(product => new
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
                status = product.Status,
                order = product.SortOrder,
            }));
        });

        group.MapGet("/settings", async (CatalogReadService catalog, CancellationToken cancellationToken) =>
        {
            var settings = await catalog.GetSettingsAsync(cancellationToken);
            return Results.Ok(new { whatsappNumber = settings.WhatsappNumber });
        });

        group.MapPost("/media/{category}", async (string category, IFormFile file, ICatalogMediaStorage storage, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(new { url = await storage.UploadAsync(category, file, ct) });
            }
            catch (CatalogMediaValidationException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [exception.Message] });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(title: "Armazenamento de mídia não configurado", detail: exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (AmazonS3Exception)
            {
                return Results.Problem(title: "Não foi possível armazenar a mídia", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).DisableAntiforgery();

        group.MapPost("/types", async (TypeRequest request, ICatalogWriteRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || !new[] { "shop", "suppliers", "both" }.Contains(request.Scope)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["Informe nome e aplicação válidos."] });
            var saved = await repo.SaveTypeAsync(new CatalogType(request.Id, request.Name.Trim(), request.Scope, request.Active) { Translations = request.Translations }, ct);
            return Results.Ok(new { id=saved.Id, name=saved.Name, scope=saved.Scope, active=saved.IsActive });
        });
        group.MapPut("/types/{id:guid}", async (Guid id, TypeRequest request, ICatalogWriteRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || !new[] { "shop", "suppliers", "both" }.Contains(request.Scope)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["Informe nome e aplicação válidos."] });
            var saved = await repo.SaveTypeAsync(new CatalogType(id, request.Name.Trim(), request.Scope, request.Active) { Translations = request.Translations }, ct);
            return Results.Ok(new { id=saved.Id, name=saved.Name, scope=saved.Scope, active=saved.IsActive });
        });
        group.MapDelete("/types/{id:guid}", async (Guid id, CatalogReadService catalog, ICatalogWriteRepository repo, CancellationToken ct) =>
        {
            var isInUse = (await catalog.GetAllShopProductsAsync(ct)).Any(product => product.TypeId == id)
                || (await catalog.GetAllAffiliateProductsAsync(ct)).Any(product => product.TypeId == id);
            if (isInUse)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["Este tipo possui cadastros vinculados e não pode ser excluído."] });
            await repo.DeleteTypeAsync(id, ct);
            return Results.NoContent();
        });
        group.MapPut("/products/order", (CatalogOrderRequest request, ICatalogWriteRepository repo, CancellationToken ct) => Reorder(request, false, repo, ct));
        group.MapPut("/affiliates/order", (CatalogOrderRequest request, ICatalogWriteRepository repo, CancellationToken ct) => Reorder(request, true, repo, ct));
        group.MapPost("/products", (ProductRequest request, CatalogReadService catalog, ICatalogWriteRepository repo, CancellationToken ct) => SaveProduct(request, Guid.Empty, catalog, repo, ct));
        group.MapPut("/products/{id:guid}", (Guid id, ProductRequest request, CatalogReadService catalog, ICatalogWriteRepository repo, CancellationToken ct) => SaveProduct(request, id, catalog, repo, ct));
        group.MapDelete("/products/{id:guid}", async (Guid id, ICatalogWriteRepository repo, CancellationToken ct) => { await repo.DeleteShopProductAsync(id,ct); return Results.NoContent(); });
        group.MapPost("/affiliates", (AffiliateRequest request, ICatalogWriteRepository repo, IPlatformRepository platforms, CatalogReadService catalog, CancellationToken ct) => SaveAffiliate(request, Guid.Empty, repo, platforms, catalog, ct));
        group.MapPut("/affiliates/{id:guid}", (Guid id, AffiliateRequest request, ICatalogWriteRepository repo, IPlatformRepository platforms, CatalogReadService catalog, CancellationToken ct) => SaveAffiliate(request, id, repo, platforms, catalog, ct));
        group.MapDelete("/affiliates/{id:guid}", async (Guid id, ICatalogWriteRepository repo, CancellationToken ct) => { await repo.DeleteAffiliateProductAsync(id,ct); return Results.NoContent(); });
        group.MapPut("/settings", async (SettingsRequest request, ICatalogWriteRepository repo, CancellationToken ct) => { if (!string.IsNullOrEmpty(request.WhatsappNumber) && !System.Text.RegularExpressions.Regex.IsMatch(request.WhatsappNumber,"^[1-9]\\d{7,14}$")) return Results.ValidationProblem(new Dictionary<string,string[]> { ["whatsappNumber"]=["Informe somente dígitos do número internacional."] }); await repo.SaveSettingsAsync(new CatalogSettings(request.WhatsappNumber),ct); return Results.NoContent(); });
    }

    private static IResult ThemeProblem(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["themes"] = [message] });

    private static async Task<IResult> SaveTheme(ThemeRequest request, Guid id, ICatalogWriteRepository repo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
            return ThemeProblem("Informe um nome de tema com até 120 caracteres.");
        try
        {
            var saved = await repo.SaveThemeAsync(new CatalogTheme(id, request.Name.Trim(), request.Active) { Translations = request.Translations }, ct);
            return Results.Ok(new { id = saved.Id, name = saved.Name, active = saved.IsActive });
        }
        catch (CatalogThemeValidationException ex) { return ThemeProblem(ex.Message); }
    }

    private static async Task<IResult> Reorder(CatalogOrderRequest request, bool suppliers, ICatalogWriteRepository repo, CancellationToken ct)
    {
        if (request.Ids is null || request.Expected is null || request.Ids.Length == 0 || request.Ids.Any(id => id == Guid.Empty) || request.Ids.Distinct().Count() != request.Ids.Length)
            return Results.Problem(detail: "A sequência informada é inválida.", statusCode: 400);
        var saved = await repo.ReorderAsync(suppliers, request.Ids, request.Expected, ct);
        return saved ? Results.NoContent() : Results.Problem(detail: "O catálogo foi alterado enquanto você organizava. Cancele a organização, atualize a página e tente novamente.", statusCode: 409);
    }
    private static async Task<IResult> SaveProduct(ProductRequest r, Guid id, CatalogReadService catalog, ICatalogWriteRepository repo, CancellationToken ct)
    {
        var images = r.Images ?? [];
        if (r.TypeId == Guid.Empty || string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Description) || r.Order < 0 ||
            !new[] { "draft", "published", "inactive" }.Contains(r.Status) || !new[] { "consult", "fixed", "from" }.Contains(r.PriceMode) ||
            (r.PriceMode != "consult" && (!r.Price.HasValue || r.Price <= 0)) || images.Length > 6 || images.Any(url => !IsAllowedImageUrl(url)) ||
            (r.Status == "published" && images.Length == 0))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Confira os campos obrigatórios, a foto principal e os endereços das imagens."] });

        if (r.Featured && r.Status != "published")
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Um produto em destaque precisa estar publicado."] });
        if (r.Featured && await repo.CountPublishedFeaturedShopProductsAsync(id, ct) >= 3)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Escolha no máximo três produtos em destaque."] });

        var rootSlug = ProductSlug.From(string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug);
        if (string.IsNullOrWhiteSpace(rootSlug))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Informe um título que permita criar a URL do produto."] });
        var allProducts = await catalog.GetAllShopProductsAsync(ct);
        var themeIds = r.ThemeIds ?? allProducts.FirstOrDefault(p => p.Id == id)?.ThemeIds.ToArray() ?? [];
        if (themeIds.Length > 50 || themeIds.Any(themeId => themeId == Guid.Empty))
            return ThemeProblem("Selecione até 50 temas válidos.");
        var usedSlugs = allProducts.Where(product => product.Id != id).Select(product => product.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var slug = rootSlug;
        for (var suffix = 2; usedSlugs.Contains(slug); suffix++) slug = $"{rootSlug}-{suffix}";
        try
        {
            var product = await repo.SaveShopProductAsync(new ShopProduct(id, r.TypeId, r.Name.Trim(), slug, r.Description.Trim(), r.FullDescription?.Trim(), r.PriceMode, r.Price, r.Demo, r.Featured, r.Characteristics ?? [], r.Personalization ?? [], images.Select((url, index) => new ProductImage(Guid.NewGuid(), url, index)).ToArray(), r.Status, r.Order) { ThemeIds = themeIds.Distinct().ToArray() }, ct);
            return Results.Ok(new { id = product.Id });
        }
        catch (CatalogThemeValidationException ex) { return ThemeProblem(ex.Message); }
    }

    private static async Task<IResult> SaveAffiliate(AffiliateRequest r, Guid id, ICatalogWriteRepository repo, IPlatformRepository platforms, CatalogReadService catalog, CancellationToken ct)
    {
        var images = r.Images ?? (string.IsNullOrWhiteSpace(r.Image) ? [] : new[] { r.Image });
        if (r.TypeId == Guid.Empty || string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Description) || r.Order < 0 ||
            !new[] { "draft", "published", "inactive" }.Contains(r.Status) || string.IsNullOrWhiteSpace(r.Platform) ||
            (images.Length > 5 || images.Any(image => !IsAllowedImageUrl(image))) || (r.Url is not null && !IsHttpsUrl(r.Url)) ||
            (r.Status == "published" && !r.DemoListing && (images.Length == 0 || string.IsNullOrWhiteSpace(r.Url))))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["affiliate"] = ["Confira os campos obrigatórios, a foto principal e o link HTTPS da indicação."] });

        if (r.Featured && r.Status != "published")
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["affiliate"] = ["Uma indicação em destaque precisa estar publicada."] });
        if (r.Featured && await repo.CountPublishedFeaturedAffiliateProductsAsync(id, ct) >= 3)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["affiliate"] = ["Escolha no máximo três fornecedores em destaque."] });

        var registeredPlatforms = await platforms.GetAsync(null, null, false, false, ct);
        var selectedPlatform = r.PlatformId.HasValue
            ? registeredPlatforms.FirstOrDefault(p => p.Id == r.PlatformId)
            : registeredPlatforms.OrderByDescending(p => p.Locale == "pt-BR" && p.CountryCode == "BR").FirstOrDefault(p => p.Name == r.Platform);
        if (selectedPlatform is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["platform"] = ["Selecione uma plataforma cadastrada."] });
        if (!selectedPlatform.Active && !(await catalog.GetAllAffiliateProductsAsync(ct)).Any(p => p.Id == id && p.PlatformId == selectedPlatform.Id))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["platform"] = ["Selecione uma plataforma ativa."] });
        var affiliate = await repo.SaveAffiliateProductAsync(new AffiliateProduct(id, r.TypeId, r.Name.Trim(), r.Description.Trim(), selectedPlatform.Name, images.FirstOrDefault()?.Trim(), r.Url?.Trim(), r.Seller?.Trim(), r.DemoListing, r.Featured, r.Status, r.Order) { Images = images.Select(image => image.Trim()).ToArray(), IsInternational = r.International, PlatformId = selectedPlatform.Id, Translations = r.Translations }, ct);
        return Results.Ok(new { id = affiliate.Id });
    }

    internal static bool IsAllowedImageUrl(string value) =>
        value.StartsWith("/assets/", StringComparison.Ordinal) || IsHttpsUrl(value);

    internal static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
}

public sealed record TypeRequest(Guid Id, string Name, string Scope, bool Active) : ITranslationInput { public Dictionary<string,CatalogTranslation>? Translations { get; init; } }
public sealed record ProductRequest(Guid TypeId,string Name,string? Slug,string Description,string? FullDescription,string PriceMode,decimal? Price,bool Demo,bool Featured,string[]? Characteristics,string[]? Personalization,string[]? Images,string Status,int Order,Guid[]? ThemeIds = null);
public sealed record AffiliateRequest(Guid TypeId,string Name,string Description,string Platform,string? Image,string? Url,string? Seller,bool DemoListing,bool Featured,string Status,int Order,string[]? Images = null,bool International = false,Guid? PlatformId = null) : ITranslationInput { public Dictionary<string,CatalogTranslation>? Translations { get; init; } }
public sealed record SettingsRequest(string WhatsappNumber);

public sealed record CatalogOrderRequest(Guid[]? Ids, CatalogOrderEntry[]? Expected);

public sealed record ThemeRequest(string Name, bool Active) : ITranslationInput { public Dictionary<string,CatalogTranslation>? Translations { get; init; } }
