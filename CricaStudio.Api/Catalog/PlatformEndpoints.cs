using CricaStudio.Domain.Catalog;
using System.Text.RegularExpressions;
using System.Globalization;
namespace CricaStudio.Api.Catalog;
public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin/catalog/platforms").RequireAuthorization().AddEndpointFilter<TranslationEndpointFilter>();
        admin.MapGet("/", (IPlatformRepository repo, CancellationToken ct) => repo.GetAsync(null, null, false, false, ct));
        admin.MapPost("/", (CatalogPlatform item, IPlatformRepository repo, CancellationToken ct) => Save(item with { Id = Guid.NewGuid() }, true, repo, ct));
        admin.MapPut("/{id:guid}", (Guid id, CatalogPlatform item, IPlatformRepository repo, CancellationToken ct) => Save(item with { Id = id }, false, repo, ct));
        admin.MapDelete("/{id:guid}", async (Guid id, IPlatformRepository repo, CancellationToken ct) =>
        {
            try { await repo.DeleteAsync(id, ct); return Results.NoContent(); }
            catch (PlatformValidationException ex) { return Problem(ex.Message); }
        });
        app.MapGet("/api/catalog/platforms", async (string? locale, string? country, bool? storefronts, bool? allMarkets, string? market, IPlatformRepository repo, CancellationToken ct) =>
        {
            if (!CatalogEndpoints.TryMarketScope(market, out _)) return Problem("Selecione o catálogo brasileiro ou internacional.");
            if (!TryMarket(locale ?? "pt-BR", country ?? "BR", out var language, out var region)) return Problem("Informe um idioma e país válidos.");
            var items = await ReadPublic(repo, language, region, storefronts == true, allMarkets == true, ct, market ?? "br");
            // Supplier filters need platform identity, not unpublished storefront content.
            return Results.Ok(storefronts == true ? items : items.Select(p => p with { Description = "", Url = null, ProductCount = 0 }).ToArray());
        }).AddEndpointFilter<TranslationEndpointFilter>();
    }
    internal static async Task<IReadOnlyList<CatalogPlatform>> ReadPublic(IPlatformRepository repo, string locale, string country, bool storefronts, bool allMarkets, CancellationToken ct, string? market = null)
    {
        if (market is not null)
            return (await repo.GetAsync(null, null, false, storefronts, ct)).Where(p => p.Active
                && (!storefronts || p.Status == "published")
                && (p.Locale.Equals("pt-BR", StringComparison.OrdinalIgnoreCase) == (market == "br"))).ToArray();
        // The general storefront directory includes international stores; supplier filters stay local.
        if (storefronts && allMarkets)
            return (await repo.GetAsync(null, null, false, true, ct)).Where(p => p.Active && p.Status == "published").ToArray();
        return await repo.GetAsync(locale, country, true, storefronts, ct);
    }
    internal static bool SafeUrl(string? value) => string.IsNullOrEmpty(value) || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) && !string.IsNullOrEmpty(uri.Host));
    internal static bool SafeLogoUrl(string? value) => SafeUrl(value) || value is
        "/assets/construction/shopee.svg" or "/assets/construction/mercado-livre.svg" or "/assets/construction/tiktok-shop.png" or
        "/assets/platforms/aliexpress.svg" or "/assets/platforms/amazon.svg" or "/assets/platforms/temu.svg";
    internal static bool TryMarket(string? locale, string? country, out string normalizedLocale, out string normalizedCountry)
    {
        normalizedLocale = normalizedCountry = "";
        if (string.IsNullOrWhiteSpace(locale) || locale.Length > 35 || string.IsNullOrWhiteSpace(country)) return false;
        try
        {
            var culture = CultureInfo.GetCultureInfo(locale);
            if (culture.IsNeutralCulture || string.IsNullOrEmpty(culture.Name)) return false;
            normalizedLocale = culture.Name;
            normalizedCountry = country.Trim().ToUpperInvariant();
            if (normalizedCountry == "*") return true;
            return Regex.IsMatch(normalizedCountry, "^[A-Z]{2}$") && new RegionInfo(normalizedCountry).TwoLetterISORegionName == normalizedCountry;
        }
        catch (ArgumentException) { return false; }
    }
    private static IResult Problem(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["platform"] = [message] });
    internal static async Task<IResult> Save(CatalogPlatform item, bool create, IPlatformRepository repo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 120 || string.IsNullOrWhiteSpace(item.Code)
            || !Regex.IsMatch(item.Code, "^[a-z0-9][a-z0-9-]{0,39}$") || item.Description is null || item.Description.Length > 500
            || !SafeUrl(item.Url) || !SafeLogoUrl(item.LogoUrl) || item.Url?.Length > 2048 || item.LogoUrl?.Length > 2048
            || item.Order < 0 || !new[] { "draft", "published", "inactive" }.Contains(item.Status)
            || !TryMarket(item.Locale, item.CountryCode, out var locale, out var country))
            return Problem("Confira nome, código, descrição, links HTTPS, idioma, país, ordem e status.");
        if (item.Status == "published" && string.IsNullOrWhiteSpace(item.Description)) return Problem("Inclua uma descrição para exibir a plataforma nas vitrines.");
        try
        {
            var saved = await repo.SaveAsync(item with { Name = item.Name.Trim(), Description = item.Description.Trim(),
                Url = string.IsNullOrWhiteSpace(item.Url) ? null : item.Url.Trim(), LogoUrl = string.IsNullOrWhiteSpace(item.LogoUrl) ? null : item.LogoUrl.Trim(), Locale = locale, CountryCode = country }, create, ct);
            return saved is null ? Results.NotFound() : Results.Ok(saved);
        }
        catch (PlatformValidationException ex) { return Problem(ex.Message); }
    }
}
