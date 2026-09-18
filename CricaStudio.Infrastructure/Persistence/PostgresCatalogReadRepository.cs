using System.Text.Json;
using CricaStudio.Domain.Catalog;
using Npgsql;
using NpgsqlTypes;

namespace CricaStudio.Infrastructure.Persistence;

public sealed class PostgresCatalogReadRepository(string connectionString) : ICatalogReadRepository
{
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

    public async Task<CatalogPage<ShopProduct>> GetPublishedShopProductPageAsync(int page, int pageSize, string? query, Guid? typeId, bool featuredOnly, CancellationToken cancellationToken)
    {
        const string filters = """
            p.status = 'published'
            AND (@typeId IS NULL OR p.type_id = @typeId)
            AND (@featuredOnly = FALSE OR p.is_featured)
            AND (@query = '' OR translate(lower(p.name || ' ' || p.description), 'áàãâäéèêëíìîïóòõôöúùûüç', 'aaaaaeeeeiiiiooooouuuuc') LIKE '%' || @query || '%')
            """;
        var total = await CountAsync("cricastudio.shop_products p", filters, query, typeId, null, featuredOnly, cancellationToken);
        var sql = $"""
            WITH selected AS (
                SELECT p.id, p.type_id, p.name, p.description, p.full_description, p.price_mode, p.price,
                       p.is_demo, p.is_featured, p.characteristics::text AS characteristics, p.personalization::text AS personalization,
                       p.status, p.sort_order, p.created_at
                FROM cricastudio.shop_products p
                WHERE {filters}
                ORDER BY p.sort_order, p.created_at, p.id
                LIMIT @limit OFFSET @offset
            )
            SELECT p.id, p.type_id, p.name, p.description, p.full_description, p.price_mode, p.price,
                   p.is_demo, p.is_featured, p.characteristics, p.personalization, p.status, p.sort_order,
                   i.id, i.image_url, i.sort_order
            FROM selected p
            LEFT JOIN cricastudio.shop_product_images i ON i.product_id = p.id
            ORDER BY p.sort_order, p.created_at, p.id, i.sort_order, i.created_at;
            """;
        var products = new List<ShopProduct>();
        var imagesByProduct = new Dictionary<Guid, List<ProductImage>>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        AddPageParameters(command, page, pageSize, query, typeId, null, featuredOnly);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(0);
            if (!imagesByProduct.TryGetValue(id, out var images))
            {
                images = [];
                imagesByProduct[id] = images;
                products.Add(new ShopProduct(
                    id, reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetDecimal(6), reader.GetBoolean(7), reader.GetBoolean(8),
                    DeserializeStrings(reader.GetString(9)), DeserializeStrings(reader.GetString(10)), images,
                    reader.GetString(11), reader.GetInt32(12)));
            }
            if (!reader.IsDBNull(13)) images.Add(new ProductImage(reader.GetGuid(13), reader.GetString(14), reader.GetInt32(15)));
        }
        return new CatalogPage<ShopProduct>(products, total);
    }

    public async Task<IReadOnlyList<ShopProduct>> GetAllShopProductsAsync(CancellationToken cancellationToken)
    {
        return await GetShopProductsAsync(onlyPublished: false, cancellationToken);
    }

    private async Task<IReadOnlyList<ShopProduct>> GetShopProductsAsync(bool onlyPublished, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT p.id, p.type_id, p.name, p.description, p.full_description, p.price_mode, p.price,
                    p.is_demo, p.is_featured, p.characteristics::text, p.personalization::text, p.status, p.sort_order,
                   i.id, i.image_url, i.sort_order
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
                    id, reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetDecimal(6), reader.GetBoolean(7), reader.GetBoolean(8),
                    DeserializeStrings(reader.GetString(9)), DeserializeStrings(reader.GetString(10)), images,
                    reader.GetString(11), reader.GetInt32(12)));
            }

            if (!reader.IsDBNull(13))
                images.Add(new ProductImage(reader.GetGuid(13), reader.GetString(14), reader.GetInt32(15)));
        }
        return products;
    }

    public async Task<IReadOnlyList<AffiliateProduct>> GetPublishedAffiliateProductsAsync(CancellationToken cancellationToken)
    {
        return await GetAffiliateProductsAsync(onlyPublished: true, cancellationToken);
    }

    public async Task<CatalogPage<AffiliateProduct>> GetPublishedAffiliateProductPageAsync(int page, int pageSize, string? query, string? platform, bool featuredOnly, CancellationToken cancellationToken)
    {
        const string filters = """
            p.status = 'published'
            AND (@platform IS NULL OR p.platform = @platform)
            AND (@featuredOnly = FALSE OR p.is_featured)
            AND (@query = '' OR translate(lower(p.name || ' ' || p.description), 'áàãâäéèêëíìîïóòõôöúùûüç', 'aaaaaeeeeiiiiooooouuuuc') LIKE '%' || @query || '%')
            """;
        var total = await CountAsync("cricastudio.affiliate_products p", filters, query, null, platform, featuredOnly, cancellationToken);
        var sql = $"""
            SELECT p.id, p.type_id, p.name, p.description, p.platform, p.image_url, p.affiliate_url, p.seller,
                   p.is_demo_listing, p.is_featured, p.status, p.sort_order
            FROM cricastudio.affiliate_products p
            WHERE {filters}
            ORDER BY p.sort_order, p.created_at, p.id
            LIMIT @limit OFFSET @offset;
            """;
        var products = new List<AffiliateProduct>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        AddPageParameters(command, page, pageSize, query, null, platform, featuredOnly);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            products.Add(new AffiliateProduct(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetBoolean(8), reader.GetBoolean(9), reader.GetString(10), reader.GetInt32(11)));
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
                   is_demo_listing, is_featured, status, sort_order
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
                reader.GetBoolean(8), reader.GetBoolean(9), reader.GetString(10), reader.GetInt32(11)));
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

    private async Task<int> CountAsync(string table, string filters, string? query, Guid? typeId, string? platform, bool featuredOnly, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE {filters};", connection);
        AddPageParameters(command, 1, 1, query, typeId, platform, featuredOnly);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static void AddPageParameters(NpgsqlCommand command, int page, int pageSize, string? query, Guid? typeId, string? platform, bool featuredOnly)
    {
        command.Parameters.Add("query", NpgsqlDbType.Text).Value = query ?? string.Empty;
        command.Parameters.Add("typeId", NpgsqlDbType.Uuid).Value = (object?)typeId ?? DBNull.Value;
        command.Parameters.Add("platform", NpgsqlDbType.Text).Value = (object?)platform ?? DBNull.Value;
        command.Parameters.Add("featuredOnly", NpgsqlDbType.Boolean).Value = featuredOnly;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = pageSize;
        command.Parameters.Add("offset", NpgsqlDbType.Integer).Value = (page - 1) * pageSize;
    }
}
