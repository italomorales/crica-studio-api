using System.Text.Json;
using CricaStudio.Domain.Catalog;
using Npgsql;
using NpgsqlTypes;

namespace CricaStudio.Infrastructure.Persistence;

public sealed class PostgresCatalogReadRepository(string connectionString) : ICatalogReadRepository
{
    public async Task<IReadOnlyList<CatalogTheme>> GetThemesAsync(bool onlyPublished, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT t.id, t.name, t.is_active, count(p.id)::int
            FROM cricastudio.catalog_themes t
            LEFT JOIN cricastudio.shop_product_themes pt ON pt.theme_id = t.id
            LEFT JOIN cricastudio.shop_products p ON p.id = pt.product_id
                {(onlyPublished ? "AND p.status = 'published'" : string.Empty)}
            {(onlyPublished ? "WHERE t.is_active" : string.Empty)}
            GROUP BY t.id
            {(onlyPublished ? "HAVING count(p.id) > 0" : string.Empty)}
            ORDER BY t.name, t.id;
            """;
        var themes = new List<CatalogTheme>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            themes.Add(new CatalogTheme(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2), reader.GetInt32(3)));
        return themes;
    }

    public async Task<IReadOnlyList<CatalogType>> GetPublishedTypesAsync(CancellationToken cancellationToken)
    {
        return await GetTypesAsync(onlyActive: true, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogType>> GetAllTypesAsync(CancellationToken cancellationToken)
    {
        return await GetTypesAsync(onlyActive: false, cancellationToken);
    }

    private async Task<IReadOnlyList<CatalogType>> GetTypesAsync(bool onlyActive, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT id, name, scope, is_active
            FROM cricastudio.catalog_types
            {(onlyActive ? "WHERE is_active" : string.Empty)}
            ORDER BY name;
            """;

        var types = new List<CatalogType>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            types.Add(new CatalogType(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        return types;
    }

    public async Task<IReadOnlyList<ShopProduct>> GetPublishedShopProductsAsync(CancellationToken cancellationToken)
    {
        return await GetShopProductsAsync(onlyPublished: true, cancellationToken);
    }

    public async Task<CatalogPage<ShopProduct>> GetPublishedShopProductPageAsync(int page, int pageSize, string? query, Guid? typeId, bool featuredOnly, CancellationToken cancellationToken, IReadOnlyList<Guid>? themeIds = null)
    {
        const string filters = """
            p.status = 'published'
            AND (@typeId IS NULL OR p.type_id = @typeId)
            AND (cardinality(@themeIds) = 0 OR EXISTS (
                SELECT 1 FROM cricastudio.shop_product_themes pt
                JOIN cricastudio.catalog_themes t ON t.id = pt.theme_id AND t.is_active
                WHERE pt.product_id = p.id AND pt.theme_id = ANY(@themeIds)
            ))
            AND (@featuredOnly = FALSE OR p.is_featured)
            AND (@query = '' OR translate(lower(p.name || ' ' || p.description), 'áàãâäéèêëíìîïóòõôöúùûüç', 'aaaaaeeeeiiiiooooouuuuc') LIKE '%' || @query || '%')
            """;
        var total = await CountAsync("cricastudio.shop_products p", filters, query, typeId, null, featuredOnly, cancellationToken, themeIds);
        var sql = $"""
            WITH selected AS (
                SELECT p.id, p.type_id, p.name, p.slug, p.description, p.full_description, p.price_mode, p.price,
                       p.is_demo, p.is_featured, p.characteristics::text AS characteristics, p.personalization::text AS personalization,
                       p.status, p.sort_order, p.created_at
                FROM cricastudio.shop_products p
                WHERE {filters}
                ORDER BY p.sort_order, p.created_at, p.id
                LIMIT @limit OFFSET @offset
            )
            SELECT p.id, p.type_id, p.name, p.slug, p.description, p.full_description, p.price_mode, p.price,
                   p.is_demo, p.is_featured, p.characteristics, p.personalization, p.status, p.sort_order,
                   i.id, i.image_url, i.sort_order,
                   ARRAY(SELECT pt.theme_id FROM cricastudio.shop_product_themes pt
                         JOIN cricastudio.catalog_themes t ON t.id = pt.theme_id AND t.is_active
                         WHERE pt.product_id = p.id ORDER BY pt.theme_id)
            FROM selected p
            LEFT JOIN cricastudio.shop_product_images i ON i.product_id = p.id
            ORDER BY p.sort_order, p.created_at, p.id, i.sort_order, i.created_at;
            """;
        var products = new List<ShopProduct>();
        var imagesByProduct = new Dictionary<Guid, List<ProductImage>>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        AddPageParameters(command, page, pageSize, query, typeId, null, featuredOnly, themeIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(0);
            if (!imagesByProduct.TryGetValue(id, out var images))
            {
                images = [];
                imagesByProduct[id] = images;
                products.Add(new ShopProduct(
                    id, reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetDecimal(7), reader.GetBoolean(8), reader.GetBoolean(9),
                    DeserializeStrings(reader.GetString(10)), DeserializeStrings(reader.GetString(11)), images,
                    reader.GetString(12), reader.GetInt32(13)) { ThemeIds = reader.GetFieldValue<Guid[]>(17) });
            }
            if (!reader.IsDBNull(14)) images.Add(new ProductImage(reader.GetGuid(14), reader.GetString(15), reader.GetInt32(16)));
        }
        return new CatalogPage<ShopProduct>(products, total);
    }

    public async Task<IReadOnlyList<ShopProduct>> GetAllShopProductsAsync(CancellationToken cancellationToken)
    {
        return await GetShopProductsAsync(onlyPublished: false, cancellationToken);
    }

    public async Task<ShopProduct?> GetPublishedShopProductBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var products = await GetShopProductsAsync(onlyPublished: true, cancellationToken);
        return products.SingleOrDefault(product => string.Equals(product.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<ShopProduct>> GetShopProductsAsync(bool onlyPublished, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT p.id, p.type_id, p.name, p.slug, p.description, p.full_description, p.price_mode, p.price,
                    p.is_demo, p.is_featured, p.characteristics::text, p.personalization::text, p.status, p.sort_order,
                   i.id, i.image_url, i.sort_order,
                   ARRAY(SELECT pt.theme_id FROM cricastudio.shop_product_themes pt
                         JOIN cricastudio.catalog_themes t ON t.id = pt.theme_id
                         WHERE pt.product_id = p.id {(onlyPublished ? "AND t.is_active" : string.Empty)} ORDER BY pt.theme_id)
            FROM cricastudio.shop_products p
            LEFT JOIN cricastudio.shop_product_images i ON i.product_id = p.id
            {(onlyPublished ? "WHERE p.status = 'published'" : string.Empty)}
            ORDER BY p.sort_order, p.created_at, i.sort_order, i.created_at;
            """;

        var products = new List<ShopProduct>();
        var imagesByProduct = new Dictionary<Guid, List<ProductImage>>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(0);
            if (!imagesByProduct.TryGetValue(id, out var images))
            {
                images = [];
                imagesByProduct[id] = images;
                products.Add(new ShopProduct(
                    id, reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetDecimal(7), reader.GetBoolean(8), reader.GetBoolean(9),
                    DeserializeStrings(reader.GetString(10)), DeserializeStrings(reader.GetString(11)), images,
                    reader.GetString(12), reader.GetInt32(13)) { ThemeIds = reader.GetFieldValue<Guid[]>(17) });
            }

            if (!reader.IsDBNull(14))
                images.Add(new ProductImage(reader.GetGuid(14), reader.GetString(15), reader.GetInt32(16)));
        }
        return products;
    }

    public async Task<IReadOnlyList<AffiliateProduct>> GetPublishedAffiliateProductsAsync(CancellationToken cancellationToken)
    {
        return await GetAffiliateProductsAsync(onlyPublished: true, cancellationToken);
    }

    public async Task<CatalogPage<AffiliateProduct>> GetPublishedAffiliateProductPageAsync(int page, int pageSize, string? query, string? platform, bool featuredOnly, CancellationToken cancellationToken, IReadOnlyList<Guid>? typeIds = null)
    {
        const string filters = """
            p.status = 'published'
            AND (@platform IS NULL OR p.platform = @platform)
            AND (cardinality(@typeIds) = 0 OR p.type_id = ANY(@typeIds))
            AND (@featuredOnly = FALSE OR p.is_featured)
            AND (@query = '' OR translate(lower(p.name || ' ' || p.description), 'áàãâäéèêëíìîïóòõôöúùûüç', 'aaaaaeeeeiiiiooooouuuuc') LIKE '%' || @query || '%')
            """;
        var total = await CountAsync("cricastudio.affiliate_products p", filters, query, null, platform, featuredOnly, cancellationToken, typeIds: typeIds);
        var sql = $"""
            SELECT p.id, p.type_id, p.name, p.description, p.platform, p.image_url, p.affiliate_url, p.seller,
                   p.is_demo_listing, p.is_featured, p.status, p.sort_order, p.images::text, p.is_international
            FROM cricastudio.affiliate_products p
            WHERE {filters}
            ORDER BY p.sort_order, p.created_at, p.id
            LIMIT @limit OFFSET @offset;
            """;
        var products = new List<AffiliateProduct>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        AddPageParameters(command, page, pageSize, query, null, platform, featuredOnly, typeIds: typeIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            products.Add(new AffiliateProduct(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetBoolean(8), reader.GetBoolean(9), reader.GetString(10), reader.GetInt32(11)) { Images = DeserializeStrings(reader.GetString(12)), IsInternational = reader.GetBoolean(13) });
        return new CatalogPage<AffiliateProduct>(products, total);
    }

    public async Task<IReadOnlyList<AffiliateProduct>> GetAllAffiliateProductsAsync(CancellationToken cancellationToken)
    {
        return await GetAffiliateProductsAsync(onlyPublished: false, cancellationToken);
    }

    private async Task<IReadOnlyList<AffiliateProduct>> GetAffiliateProductsAsync(bool onlyPublished, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT id, type_id, name, description, platform, image_url, affiliate_url, seller,
                   is_demo_listing, is_featured, status, sort_order, images::text, is_international
            FROM cricastudio.affiliate_products
            {(onlyPublished ? "WHERE status = 'published'" : string.Empty)}
            ORDER BY sort_order, created_at;
            """;

        var products = new List<AffiliateProduct>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            products.Add(new AffiliateProduct(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetBoolean(8), reader.GetBoolean(9), reader.GetString(10), reader.GetInt32(11)) { Images = DeserializeStrings(reader.GetString(12)), IsInternational = reader.GetBoolean(13) });
        return products;
    }

    public async Task<CatalogSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT value FROM cricastudio.site_settings WHERE key = 'whatsapp_number';";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return new CatalogSettings(value as string ?? string.Empty);
    }

    private static IReadOnlyList<string> DeserializeStrings(string value) =>
        JsonSerializer.Deserialize<string[]>(value) ?? [];

    private async Task<int> CountAsync(string table, string filters, string? query, Guid? typeId, string? platform, bool featuredOnly, CancellationToken cancellationToken, IReadOnlyList<Guid>? themeIds = null, IReadOnlyList<Guid>? typeIds = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE {filters};", connection);
        AddPageParameters(command, 1, 1, query, typeId, platform, featuredOnly, themeIds, typeIds);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static void AddPageParameters(NpgsqlCommand command, int page, int pageSize, string? query, Guid? typeId, string? platform, bool featuredOnly, IReadOnlyList<Guid>? themeIds = null, IReadOnlyList<Guid>? typeIds = null)
    {
        command.Parameters.Add("typeIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = typeIds?.Distinct().ToArray() ?? [];
        command.Parameters.Add("themeIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = themeIds?.Distinct().ToArray() ?? [];
        command.Parameters.Add("query", NpgsqlDbType.Text).Value = query ?? string.Empty;
        command.Parameters.Add("typeId", NpgsqlDbType.Uuid).Value = (object?)typeId ?? DBNull.Value;
        command.Parameters.Add("platform", NpgsqlDbType.Text).Value = (object?)platform ?? DBNull.Value;
        command.Parameters.Add("featuredOnly", NpgsqlDbType.Boolean).Value = featuredOnly;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = pageSize;
        command.Parameters.Add("offset", NpgsqlDbType.Integer).Value = (page - 1) * pageSize;
    }
}
