using CricaStudio.Application.Catalog;
using CricaStudio.Domain.Catalog;
using Amazon.S3;
using Microsoft.AspNetCore.Authorization;

namespace CricaStudio.Api.Catalog;

public static class AdminCatalogEndpoints
{
    public static void MapAdminCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/catalog").RequireAuthorization().WithTags("Admin catalog");

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
                name = product.Name,
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
                platform = product.Platform,
                image = product.ImageUrl,
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
            var saved = await repo.SaveTypeAsync(new CatalogType(request.Id, request.Name.Trim(), request.Scope, request.Active), ct);
            return Results.Ok(new { id=saved.Id, name=saved.Name, scope=saved.Scope, active=saved.IsActive });
        });
        group.MapPut("/types/{id:guid}", async (Guid id, TypeRequest request, ICatalogWriteRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || !new[] { "shop", "suppliers", "both" }.Contains(request.Scope)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["Informe nome e aplicação válidos."] });
            var saved = await repo.SaveTypeAsync(new CatalogType(id, request.Name.Trim(), request.Scope, request.Active), ct);
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
        group.MapPost("/products", (ProductRequest request, ICatalogWriteRepository repo, CancellationToken ct) => SaveProduct(request, Guid.Empty, repo, ct));
        group.MapPut("/products/{id:guid}", (Guid id, ProductRequest request, ICatalogWriteRepository repo, CancellationToken ct) => SaveProduct(request, id, repo, ct));
        group.MapDelete("/products/{id:guid}", async (Guid id, ICatalogWriteRepository repo, CancellationToken ct) => { await repo.DeleteShopProductAsync(id,ct); return Results.NoContent(); });
        group.MapPost("/affiliates", (AffiliateRequest request, ICatalogWriteRepository repo, CancellationToken ct) => SaveAffiliate(request, Guid.Empty, repo, ct));
        group.MapPut("/affiliates/{id:guid}", (Guid id, AffiliateRequest request, ICatalogWriteRepository repo, CancellationToken ct) => SaveAffiliate(request, id, repo, ct));
        group.MapDelete("/affiliates/{id:guid}", async (Guid id, ICatalogWriteRepository repo, CancellationToken ct) => { await repo.DeleteAffiliateProductAsync(id,ct); return Results.NoContent(); });
        group.MapPut("/settings", async (SettingsRequest request, ICatalogWriteRepository repo, CancellationToken ct) => { if (!string.IsNullOrEmpty(request.WhatsappNumber) && !System.Text.RegularExpressions.Regex.IsMatch(request.WhatsappNumber,"^[1-9]\\d{7,14}$")) return Results.ValidationProblem(new Dictionary<string,string[]> { ["whatsappNumber"]=["Informe somente dígitos do número internacional."] }); await repo.SaveSettingsAsync(new CatalogSettings(request.WhatsappNumber),ct); return Results.NoContent(); });
    }

    private static async Task<IResult> SaveProduct(ProductRequest r, Guid id, ICatalogWriteRepository repo, CancellationToken ct)
    {
        var images = r.Images ?? [];
        if (r.TypeId == Guid.Empty || string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Description) || r.Order < 0 ||
            !new[] { "draft", "published", "inactive" }.Contains(r.Status) || !new[] { "consult", "fixed", "from" }.Contains(r.PriceMode) ||
            (r.PriceMode != "consult" && (!r.Price.HasValue || r.Price <= 0)) || images.Length > 5 || images.Any(url => !IsAllowedImageUrl(url)) ||
            (r.Status == "published" && images.Length == 0))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Confira os campos obrigatórios, a foto principal e os endereços das imagens."] });

        if (r.Featured && r.Status != "published")
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Um produto em destaque precisa estar publicado."] });
        if (r.Featured && await repo.CountPublishedFeaturedShopProductsAsync(id, ct) >= 3)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Escolha no máximo três produtos em destaque."] });

        var product = await repo.SaveShopProductAsync(new ShopProduct(id, r.TypeId, r.Name.Trim(), r.Description.Trim(), r.FullDescription?.Trim(), r.PriceMode, r.Price, r.Demo, r.Featured, r.Characteristics ?? [], r.Personalization ?? [], images.Select((url, index) => new ProductImage(Guid.NewGuid(), url, index)).ToArray(), r.Status, r.Order), ct);
        return Results.Ok(new { id = product.Id });
    }

    private static async Task<IResult> SaveAffiliate(AffiliateRequest r, Guid id, ICatalogWriteRepository repo, CancellationToken ct)
    {
        if (r.TypeId == Guid.Empty || string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Description) || r.Order < 0 ||
            !new[] { "draft", "published", "inactive" }.Contains(r.Status) || !new[] { "Shopee", "Mercado Livre", "TikTok Shop", "AliExpress", "Outra" }.Contains(r.Platform) ||
            (r.Image is not null && !IsAllowedImageUrl(r.Image)) || (r.Url is not null && !IsHttpsUrl(r.Url)) ||
            (r.Status == "published" && !r.DemoListing && (string.IsNullOrWhiteSpace(r.Image) || string.IsNullOrWhiteSpace(r.Url))))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["affiliate"] = ["Confira os campos obrigatórios, a foto principal e o link HTTPS da indicação."] });

        if (r.Featured && r.Status != "published")
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["affiliate"] = ["Uma indicação em destaque precisa estar publicada."] });
        if (r.Featured && await repo.CountPublishedFeaturedAffiliateProductsAsync(id, ct) >= 3)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["affiliate"] = ["Escolha no máximo três fornecedores em destaque."] });

        var affiliate = await repo.SaveAffiliateProductAsync(new AffiliateProduct(id, r.TypeId, r.Name.Trim(), r.Description.Trim(), r.Platform, r.Image?.Trim(), r.Url?.Trim(), r.Seller?.Trim(), r.DemoListing, r.Featured, r.Status, r.Order), ct);
        return Results.Ok(new { id = affiliate.Id });
    }

    internal static bool IsAllowedImageUrl(string value) =>
        value.StartsWith("/assets/", StringComparison.Ordinal) || IsHttpsUrl(value);

    internal static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
}

public sealed record TypeRequest(Guid Id, string Name, string Scope, bool Active);
public sealed record ProductRequest(Guid TypeId,string Name,string Description,string? FullDescription,string PriceMode,decimal? Price,bool Demo,bool Featured,string[]? Characteristics,string[]? Personalization,string[]? Images,string Status,int Order);
public sealed record AffiliateRequest(Guid TypeId,string Name,string Description,string Platform,string? Image,string? Url,string? Seller,bool DemoListing,bool Featured,string Status,int Order);
public sealed record SettingsRequest(string WhatsappNumber);
