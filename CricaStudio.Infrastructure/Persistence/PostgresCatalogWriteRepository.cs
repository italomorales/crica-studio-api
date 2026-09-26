using System.Text.Json;
using CricaStudio.Domain.Catalog;
using Npgsql;

namespace CricaStudio.Infrastructure.Persistence;

public sealed class PostgresCatalogWriteRepository(string connectionString) : ICatalogWriteRepository
{
    // The entire sequence is committed atomically. Reject a stale list instead of
    // overwriting another administrator's ordering or omitting a new record.
    public async Task<bool> ReorderAsync(bool suppliers, IReadOnlyList<Guid> ids, IReadOnlyList<CatalogOrderEntry> expected, CancellationToken ct)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count || expected.Count != ids.Count || expected.Select(x => x.Id).Distinct().Count() != expected.Count || !ids.ToHashSet().SetEquals(expected.Select(x => x.Id))) return false;
        var table = suppliers ? "affiliate_products" : "shop_products";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var command = new NpgsqlCommand($"LOCK TABLE cricastudio.{table} IN SHARE ROW EXCLUSIVE MODE", connection, transaction)) await command.ExecuteNonQueryAsync(ct);
        var current = new Dictionary<Guid, int>();
        await using (var command = new NpgsqlCommand($"SELECT id, sort_order FROM cricastudio.{table}", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) current.Add(reader.GetGuid(0), reader.GetInt32(1));
        if (current.Count != expected.Count || expected.Any(item => !current.TryGetValue(item.Id, out var order) || order != item.Order)) return false;
        await using (var command = new NpgsqlCommand($"UPDATE cricastudio.{table} AS p SET sort_order = ordered.position::int FROM unnest(@ids::uuid[]) WITH ORDINALITY AS ordered(id, position) WHERE p.id = ordered.id", connection, transaction))
        {
            command.Parameters.AddWithValue("ids", ids.ToArray());
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return true;
    }
    public async Task<CatalogType> SaveTypeAsync(CatalogType type, CancellationToken cancellationToken)
    {
        var id = type.Id == Guid.Empty ? Guid.NewGuid() : type.Id;
        const string sql = """INSERT INTO cricastudio.catalog_types (id,name,scope,is_active) VALUES (@id,@name,@scope,@active) ON CONFLICT (id) DO UPDATE SET name=EXCLUDED.name,scope=EXCLUDED.scope,is_active=EXCLUDED.is_active RETURNING id,name,scope,is_active;""";
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("name", type.Name); cmd.Parameters.AddWithValue("scope", type.Scope); cmd.Parameters.AddWithValue("active", type.IsActive);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken); await r.ReadAsync(cancellationToken);
        return new CatalogType(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetBoolean(3));
    }
    public async Task<ShopProduct> SaveShopProductAsync(ShopProduct product, CancellationToken cancellationToken)
    {
        var id = product.Id == Guid.Empty ? Guid.NewGuid() : product.Id;
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(cancellationToken); await using var tx = await c.BeginTransactionAsync(cancellationToken);
        const string sql = """INSERT INTO cricastudio.shop_products (id,type_id,name,description,full_description,price_mode,price,is_demo,is_featured,characteristics,personalization,status,sort_order) VALUES (@id,@typeId,@name,@description,@fullDescription,@priceMode,@price,@demo,@featured,@characteristics::jsonb,@personalization::jsonb,@status,@order) ON CONFLICT (id) DO UPDATE SET type_id=EXCLUDED.type_id,name=EXCLUDED.name,description=EXCLUDED.description,full_description=EXCLUDED.full_description,price_mode=EXCLUDED.price_mode,price=EXCLUDED.price,is_demo=EXCLUDED.is_demo,is_featured=EXCLUDED.is_featured,characteristics=EXCLUDED.characteristics,personalization=EXCLUDED.personalization,status=EXCLUDED.status,sort_order=EXCLUDED.sort_order;""";
        await using (var cmd = new NpgsqlCommand(sql, c, tx)) { AddProductParameters(cmd, product, id); await cmd.ExecuteNonQueryAsync(cancellationToken); }
        await using (var cmd = new NpgsqlCommand("DELETE FROM cricastudio.shop_product_images WHERE product_id=@id;", c, tx)) { cmd.Parameters.AddWithValue("id", id); await cmd.ExecuteNonQueryAsync(cancellationToken); }
        foreach (var image in product.Images.Select((image, index) => new { image, index })) { await using var cmd = new NpgsqlCommand("INSERT INTO cricastudio.shop_product_images (id,product_id,image_url,sort_order) VALUES (@id,@productId,@url,@order);", c, tx); cmd.Parameters.AddWithValue("id", image.image.Id == Guid.Empty ? Guid.NewGuid() : image.image.Id); cmd.Parameters.AddWithValue("productId", id); cmd.Parameters.AddWithValue("url", image.image.Url); cmd.Parameters.AddWithValue("order", image.index); await cmd.ExecuteNonQueryAsync(cancellationToken); }
        await tx.CommitAsync(cancellationToken);
        return product with { Id = id, Images = product.Images.Select((image, index) => image with { SortOrder = index }).ToArray() };
    }
    public async Task<AffiliateProduct> SaveAffiliateProductAsync(AffiliateProduct product, CancellationToken cancellationToken)
    {
        var id = product.Id == Guid.Empty ? Guid.NewGuid() : product.Id;
        const string sql = """INSERT INTO cricastudio.affiliate_products (id,type_id,name,description,platform,images,image_url,affiliate_url,seller,is_demo_listing,is_featured,status,sort_order) VALUES (@id,@typeId,@name,@description,@platform,@images::jsonb,@image,@url,@seller,@demo,@featured,@status,@order) ON CONFLICT (id) DO UPDATE SET type_id=EXCLUDED.type_id,name=EXCLUDED.name,description=EXCLUDED.description,platform=EXCLUDED.platform,images=EXCLUDED.images,image_url=EXCLUDED.image_url,affiliate_url=EXCLUDED.affiliate_url,seller=EXCLUDED.seller,is_demo_listing=EXCLUDED.is_demo_listing,is_featured=EXCLUDED.is_featured,status=EXCLUDED.status,sort_order=EXCLUDED.sort_order;""";
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(cancellationToken); await using var cmd = new NpgsqlCommand(sql,c);
        cmd.Parameters.AddWithValue("id",id); cmd.Parameters.AddWithValue("typeId",product.TypeId); cmd.Parameters.AddWithValue("name",product.Name); cmd.Parameters.AddWithValue("description",product.Description); cmd.Parameters.AddWithValue("platform",product.Platform); cmd.Parameters.AddWithValue("images",JsonSerializer.Serialize(product.Images)); cmd.Parameters.AddWithValue("image",(object?)product.ImageUrl??DBNull.Value); cmd.Parameters.AddWithValue("url",(object?)product.AffiliateUrl??DBNull.Value); cmd.Parameters.AddWithValue("seller",(object?)product.Seller??DBNull.Value); cmd.Parameters.AddWithValue("demo",product.IsDemoListing); cmd.Parameters.AddWithValue("featured",product.IsFeatured); cmd.Parameters.AddWithValue("status",product.Status); cmd.Parameters.AddWithValue("order",product.SortOrder); await cmd.ExecuteNonQueryAsync(cancellationToken); return product with { Id=id };
    }
    public async Task<int> CountPublishedFeaturedShopProductsAsync(Guid excludingId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT count(*) FROM cricastudio.shop_products WHERE status = 'published' AND is_featured AND id <> @id;";
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("id", excludingId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }
    public async Task<int> CountPublishedFeaturedAffiliateProductsAsync(Guid excludingId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT count(*) FROM cricastudio.affiliate_products WHERE status = 'published' AND is_featured AND id <> @id;";
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("id", excludingId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }
    public async Task SaveSettingsAsync(CatalogSettings settings, CancellationToken cancellationToken) { await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(cancellationToken); await using var cmd = new NpgsqlCommand("INSERT INTO cricastudio.site_settings (key,value) VALUES ('whatsapp_number',@value) ON CONFLICT (key) DO UPDATE SET value=EXCLUDED.value;",c); cmd.Parameters.AddWithValue("value",settings.WhatsappNumber); await cmd.ExecuteNonQueryAsync(cancellationToken); }
    public Task DeleteTypeAsync(Guid id, CancellationToken ct) => DeleteAsync("catalog_types",id,ct);
    public Task DeleteShopProductAsync(Guid id, CancellationToken ct) => DeleteAsync("shop_products",id,ct);
    public Task DeleteAffiliateProductAsync(Guid id, CancellationToken ct) => DeleteAsync("affiliate_products",id,ct);
    private async Task DeleteAsync(string table, Guid id, CancellationToken ct) { await using var c=new NpgsqlConnection(connectionString); await c.OpenAsync(ct); await using var cmd=new NpgsqlCommand($"DELETE FROM cricastudio.{table} WHERE id=@id;",c); cmd.Parameters.AddWithValue("id",id); await cmd.ExecuteNonQueryAsync(ct); }
    private static void AddProductParameters(NpgsqlCommand cmd, ShopProduct p, Guid id) { cmd.Parameters.AddWithValue("id",id); cmd.Parameters.AddWithValue("typeId",p.TypeId); cmd.Parameters.AddWithValue("name",p.Name); cmd.Parameters.AddWithValue("description",p.Description); cmd.Parameters.AddWithValue("fullDescription",(object?)p.FullDescription??DBNull.Value); cmd.Parameters.AddWithValue("priceMode",p.PriceMode); cmd.Parameters.AddWithValue("price",(object?)p.Price??DBNull.Value); cmd.Parameters.AddWithValue("demo",p.IsDemo); cmd.Parameters.AddWithValue("featured",p.IsFeatured); cmd.Parameters.AddWithValue("characteristics",JsonSerializer.Serialize(p.Characteristics)); cmd.Parameters.AddWithValue("personalization",JsonSerializer.Serialize(p.Personalization)); cmd.Parameters.AddWithValue("status",p.Status); cmd.Parameters.AddWithValue("order",p.SortOrder); }
}
