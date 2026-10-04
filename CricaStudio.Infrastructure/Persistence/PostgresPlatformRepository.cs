using CricaStudio.Domain.Catalog;
using Npgsql;
using NpgsqlTypes;

namespace CricaStudio.Infrastructure.Persistence;
public sealed class PostgresPlatformRepository(string connectionString) : IPlatformRepository
{
    public async Task<IReadOnlyList<CatalogPlatform>> GetAsync(string? locale, string? country, bool publicOnly, bool storefrontsOnly, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id,p.name,p.code,p.description,p.url,p.logo_url,p.locale,p.country_code,p.status,p.sort_order,p.mobile_only,p.is_active,
              (SELECT count(*)::int FROM cricastudio.affiliate_products a WHERE a.platform_id=p.id) AS product_count
            FROM cricastudio.platforms p
            WHERE (NOT @public OR (p.is_active AND lower(p.locale)=lower(@locale) AND (p.country_code=@country OR p.country_code='*')))
              AND (NOT @storefronts OR p.status='published') ORDER BY p.sort_order,p.name,p.id
            """;
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("public", publicOnly); command.Parameters.AddWithValue("storefronts", storefrontsOnly);
        command.Parameters.Add("locale", NpgsqlDbType.Text).Value = (object?)locale ?? DBNull.Value;
        command.Parameters.Add("country", NpgsqlDbType.Text).Value = (object?)country ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<CatalogPlatform>();
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
            reader.GetString(7), reader.GetString(8), reader.GetInt32(9), reader.GetBoolean(10), reader.GetBoolean(11)) { ProductCount = reader.GetInt32(12) });
        return rows;
    }
    public async Task<CatalogPlatform?> SaveAsync(CatalogPlatform item, bool create, CancellationToken ct)
    {
        var sql = create ? """
            INSERT INTO cricastudio.platforms(id,name,code,description,url,logo_url,locale,country_code,status,sort_order,mobile_only,is_active)
            VALUES(@id,@name,@code,@description,@url,@logo,@locale,@country,@status,@order,@mobile,@active)
            """ : """
            UPDATE cricastudio.platforms SET name=@name,code=@code,description=@description,url=@url,logo_url=@logo,locale=@locale,
            country_code=@country,status=@status,sort_order=@order,mobile_only=@mobile,is_active=@active WHERE id=@id
            """;
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var transaction = System.Transactions.Transaction.Current is null ? await connection.BeginTransactionAsync(ct) : null;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", item.Id); command.Parameters.AddWithValue("name", item.Name); command.Parameters.AddWithValue("code", item.Code);
        command.Parameters.AddWithValue("description", item.Description); command.Parameters.Add("url", NpgsqlDbType.Text).Value = (object?)item.Url ?? DBNull.Value;
        command.Parameters.Add("logo", NpgsqlDbType.Text).Value = (object?)item.LogoUrl ?? DBNull.Value;
        command.Parameters.AddWithValue("locale", item.Locale); command.Parameters.AddWithValue("country", item.CountryCode); command.Parameters.AddWithValue("status", item.Status);
        command.Parameters.AddWithValue("order", item.Order); command.Parameters.AddWithValue("mobile", item.MobileOnly); command.Parameters.AddWithValue("active", item.Active);
        try
        {
            if (await command.ExecuteNonQueryAsync(ct) == 0) return null;
            await using var rename = new NpgsqlCommand("UPDATE cricastudio.affiliate_products SET platform=@name WHERE platform_id=@id", connection, transaction);
            rename.Parameters.AddWithValue("name", item.Name); rename.Parameters.AddWithValue("id", item.Id);
            await rename.ExecuteNonQueryAsync(ct);
            await using var count = new NpgsqlCommand("SELECT count(*)::int FROM cricastudio.affiliate_products WHERE platform_id=@id", connection, transaction);
            count.Parameters.AddWithValue("id", item.Id);
            var productCount = (int)(await count.ExecuteScalarAsync(ct))!;
            if (transaction is not null) await transaction.CommitAsync(ct);
            return item with { ProductCount = productCount };
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new PlatformValidationException("Já existe uma plataforma com esse código, idioma e país."); }
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("DELETE FROM cricastudio.platforms WHERE id=@id", connection); command.Parameters.AddWithValue("id", id);
        try { await command.ExecuteNonQueryAsync(ct); }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation)
        { throw new PlatformValidationException("Esta plataforma possui produtos vinculados. Desative-a para preservar os vínculos."); }
    }
}
