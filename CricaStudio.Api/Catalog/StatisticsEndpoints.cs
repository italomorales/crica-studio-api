using Npgsql;

namespace CricaStudio.Api.Catalog;

public sealed record StatisticsEvent(string Type, Guid Id, string Metric, string Source);

public static class StatisticsEndpoints
{
    internal static bool Valid(StatisticsEvent e) => e.Id != Guid.Empty && e.Source is "home" or "listing" or "direct" or "article" &&
        (e.Type, e.Metric) is ("shop", "view" or "click" or "whatsapp") or ("supplier", "click") or ("article", "view");

    public static void MapStatisticsEndpoints(this WebApplication app, string? connectionString)
    {
        app.MapPost("/api/statistics/events", async (StatisticsEvent item, CancellationToken ct) => {
            if (!Valid(item)) return Results.BadRequest();
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            const string sql = """
                INSERT INTO cricastudio.statistics_daily(day,entity_type,entity_id,metric,source,count)
                SELECT (CURRENT_TIMESTAMP AT TIME ZONE 'America/Sao_Paulo')::date,@type,@id,@metric,@source,1
                WHERE (@type='shop' AND EXISTS(SELECT 1 FROM cricastudio.shop_products WHERE id=@id AND status='published'))
                   OR (@type='supplier' AND EXISTS(SELECT 1 FROM cricastudio.affiliate_products WHERE id=@id AND status='published'))
                   OR (@type='article' AND EXISTS(SELECT 1 FROM cricastudio.blog_posts p JOIN cricastudio.blog_categories c ON c.id=p.category_id JOIN cricastudio.blog_versions v ON v.post_id=p.id JOIN cricastudio.catalog_languages l ON l.code=v.language_code WHERE p.id=@id AND c.is_active AND l.is_active AND (v.status='published' OR (v.status='scheduled' AND v.publish_at<=CURRENT_TIMESTAMP))))
                ON CONFLICT(day,entity_type,entity_id,metric,source) DO UPDATE SET count=cricastudio.statistics_daily.count+1
                """;
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("type", item.Type); cmd.Parameters.AddWithValue("id", item.Id);
            cmd.Parameters.AddWithValue("metric", item.Metric); cmd.Parameters.AddWithValue("source", item.Source);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        }).RequireRateLimiting("statistics");

        app.MapGet("/api/admin/statistics", async (DateOnly? from, DateOnly? to, string? type, CancellationToken ct) => {
            if (from is null || to is null || from > to || to.Value.DayNumber - from.Value.DayNumber > 365 || type is not (null or "all" or "shop" or "supplier" or "article"))
                return Results.BadRequest(new { message = "Selecione um período válido de até 366 dias." });
            var length = to.Value.DayNumber - from.Value.DayNumber + 1;
            if (from.Value.DayNumber < length) return Results.BadRequest(new { message = "Selecione um período válido." });
            var previous = from.Value.AddDays(-length);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            const string sql = """
                SELECT s.day,s.entity_type,s.entity_id,s.metric,s.source,s.count,
                  COALESCE(p.name,a.name,(SELECT v.title FROM cricastudio.blog_versions v WHERE v.post_id=s.entity_id ORDER BY (v.language_code='pt') DESC,v.language_code LIMIT 1),'Item removido') AS name,
                  COALESCE(a.platform,'') AS platform
                FROM cricastudio.statistics_daily s
                LEFT JOIN cricastudio.shop_products p ON s.entity_type='shop' AND p.id=s.entity_id
                LEFT JOIN cricastudio.affiliate_products a ON s.entity_type='supplier' AND a.id=s.entity_id
                WHERE s.day BETWEEN @start AND @end AND (@type='all' OR s.entity_type=@type)
                ORDER BY s.day,s.entity_type,s.entity_id
                """;
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("start", previous); cmd.Parameters.AddWithValue("end", to.Value); cmd.Parameters.AddWithValue("type", type ?? "all");
            var rows = new List<object>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) rows.Add(new {
                    day=reader.GetFieldValue<DateOnly>(0), type=reader.GetString(1), id=reader.GetGuid(2),
                    metric=reader.GetString(3), source=reader.GetString(4), count=reader.GetInt64(5), name=reader.GetString(6), platform=reader.GetString(7)
                });
            const string catalogSql = """
                SELECT 'shop' AS type,id,name,'' AS platform FROM cricastudio.shop_products WHERE @type IN ('all','shop')
                UNION ALL SELECT 'supplier',id,name,platform FROM cricastudio.affiliate_products WHERE @type IN ('all','supplier')
                UNION ALL SELECT 'article',p.id,COALESCE((SELECT v.title FROM cricastudio.blog_versions v WHERE v.post_id=p.id ORDER BY (v.language_code='pt') DESC,v.language_code LIMIT 1),'Matéria sem título'),'' FROM cricastudio.blog_posts p WHERE @type IN ('all','article')
                ORDER BY name
                """;
            var catalog = new List<object>();
            await using var catalogCommand = new NpgsqlCommand(catalogSql,connection);
            catalogCommand.Parameters.AddWithValue("type",type ?? "all");
            await using (var reader = await catalogCommand.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) catalog.Add(new { type=reader.GetString(0),id=reader.GetGuid(1),name=reader.GetString(2),platform=reader.GetString(3) });
            return Results.Ok(new { from, to, previousFrom=previous, previousTo=from.Value.AddDays(-1), rows, catalog });
        }).RequireAuthorization();
    }
}
