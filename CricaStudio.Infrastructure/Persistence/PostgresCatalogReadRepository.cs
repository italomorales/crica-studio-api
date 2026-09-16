using System.Text.Json;
using CricaStudio.Domain.Catalog;
using Npgsql;

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

    public async Task<IReadOnlyList<ShopProduct>> GetAllShopProductsAsync(CancellationToken cancellationToken)
    {
        return await GetShopProductsAsync(onlyPublished: false, cancellationToken);
    }

    private async Task<IReadOnlyList<ShopProduct>> GetShopProductsAsync(bool onlyPublished, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT p.id, p.type_id, p.name, p.description, p.full_description, p.price_mode, p.price,
                   p.is_demo, p.characteristics::text, p.personalization::text, p.status, p.sort_order,
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
                    reader.IsDBNull(6) ? null : reader.GetDecimal(6), reader.GetBoolean(7),
                    DeserializeStrings(reader.GetString(8)), DeserializeStrings(reader.GetString(9)), images,
                    reader.GetString(10), reader.GetInt32(11)));
            }

            if (!reader.IsDBNull(12))
                images.Add(new ProductImage(reader.GetGuid(12), reader.GetString(13), reader.GetInt32(14)));
        }
        return products;
    }

    public async Task<IReadOnlyList<AffiliateProduct>> GetPublishedAffiliateProductsAsync(CancellationToken cancellationToken)
    {
        return await GetAffiliateProductsAsync(onlyPublished: true, cancellationToken);
    }

    public async Task<IReadOnlyList<AffiliateProduct>> GetAllAffiliateProductsAsync(CancellationToken cancellationToken)
    {
        return await GetAffiliateProductsAsync(onlyPublished: false, cancellationToken);
    }

    private async Task<IReadOnlyList<AffiliateProduct>> GetAffiliateProductsAsync(bool onlyPublished, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT id, type_id, name, description, platform, image_url, affiliate_url, seller,
                   is_demo_listing, status, sort_order
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
                reader.GetBoolean(8), reader.GetString(9), reader.GetInt32(10)));
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
}
