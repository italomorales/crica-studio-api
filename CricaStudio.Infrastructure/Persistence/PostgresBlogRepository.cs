using System.Text.Json;
using CricaStudio.Domain.Blog;
using Npgsql;
using NpgsqlTypes;

namespace CricaStudio.Infrastructure.Persistence;

public sealed class PostgresBlogRepository(string connectionString) : IBlogRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Visible = "v.status='published' OR (v.status='scheduled' AND v.publish_at<=CURRENT_TIMESTAMP)";
    private static string Versions(bool publicOnly, bool document) => $"""
        (SELECT COALESCE(jsonb_object_agg(v.language_code,jsonb_build_object(
          'title',v.title,'summary',v.summary,'slug',v.slug,'document',{(document ? "v.document" : "'{\"type\":\"doc\",\"content\":[]}'::jsonb")},
          'seoTitle',v.seo_title,'seoDescription',v.seo_description,'status',v.status,
          'publishAt',v.publish_at,'publishedAt',COALESCE(v.published_at,CASE WHEN v.status='scheduled' THEN v.publish_at END),'updatedAt',v.updated_at)), {"'{}'::jsonb"})
         FROM cricastudio.blog_versions v JOIN cricastudio.catalog_languages l ON l.code=v.language_code
         WHERE v.post_id=p.id {(publicOnly ? "AND l.is_active AND (" + Visible + ")" : "")})
        """;
    private static string Columns(bool publicOnly, bool document) => $"p.id,p.category_id,c.name,p.cover_url,p.author,p.display_date,p.is_featured,p.updated_at,{Versions(publicOnly, document)}";
    private static BlogPost Read(NpgsqlDataReader r) => new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetString(4),
        r.GetFieldValue<DateOnly>(5),r.GetBoolean(6),JsonSerializer.Deserialize<Dictionary<string,BlogVersion>>(r.GetString(8),Json)!,r.GetFieldValue<DateTimeOffset>(7));
    private async Task<NpgsqlConnection> Open(CancellationToken ct) { var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct); return c; }
    private static void Nullable(NpgsqlCommand c,string name,NpgsqlDbType type,object? value) => c.Parameters.Add(name,type).Value=value??DBNull.Value;
    public async Task<BlogPage<BlogPost>> ListAsync(string locale,int page,int pageSize,string? query,Guid? categoryId,string? status,bool publicOnly,CancellationToken ct)
    {
        var where = $"""
            FROM cricastudio.blog_posts p JOIN cricastudio.blog_categories c ON c.id=p.category_id
            LEFT JOIN cricastudio.blog_versions v ON v.post_id=p.id AND v.language_code=@locale
            WHERE (@category IS NULL OR p.category_id=@category)
             AND (@q='' OR v.search_document @@ websearch_to_tsquery('simple',@q) OR v.title ILIKE @contains ESCAPE '\')
             AND (@status='' OR COALESCE(v.status,'draft')=@status)
             {(publicOnly ? "AND c.is_active AND ("+Visible+") AND EXISTS(SELECT 1 FROM cricastudio.catalog_languages l WHERE l.code=@locale AND l.is_active)" : "")}
            """;
        await using var conn=await Open(ct);
        void Bind(NpgsqlCommand cmd) {
            cmd.Parameters.AddWithValue("locale",locale); Nullable(cmd,"category",NpgsqlDbType.Uuid,categoryId);
            cmd.Parameters.AddWithValue("q",query?.Trim()??""); cmd.Parameters.AddWithValue("status",status??"");
            cmd.Parameters.AddWithValue("contains","%"+(query??"").Trim().Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%");
        }
        await using var count = new NpgsqlCommand("SELECT count(*)::int "+where,conn); Bind(count);
        var total=(int)(await count.ExecuteScalarAsync(ct))!;
        await using var command = new NpgsqlCommand($"SELECT {Columns(publicOnly,false)} {where} ORDER BY p.is_featured DESC,COALESCE(v.published_at,v.publish_at,p.created_at) DESC,p.id LIMIT @limit OFFSET @offset",conn);
        Bind(command); command.Parameters.AddWithValue("limit",pageSize); command.Parameters.AddWithValue("offset",(page-1)*pageSize);
        var items=new List<BlogPost>(); await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) items.Add(Read(reader));
        return new(items,total,page,pageSize);
    }
    public async Task<BlogPost?> GetAsync(Guid id,bool publicOnly,CancellationToken ct)
    {
        await using var conn=await Open(ct);
        await using var cmd=new NpgsqlCommand($"SELECT {Columns(publicOnly,true)} FROM cricastudio.blog_posts p JOIN cricastudio.blog_categories c ON c.id=p.category_id WHERE p.id=@id {(publicOnly?"AND c.is_active":"")}",conn);
        cmd.Parameters.AddWithValue("id",id); await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))return null; var post=Read(r); return publicOnly&&post.Versions.Count==0?null:post;
    }
    public async Task<BlogPost?> GetBySlugAsync(string locale,string slug,CancellationToken ct)
    {
        await using var conn=await Open(ct);
        await using var cmd=new NpgsqlCommand($"SELECT v.post_id FROM cricastudio.blog_versions v JOIN cricastudio.catalog_languages l ON l.code=v.language_code WHERE l.is_active AND v.language_code=@locale AND v.slug=@slug AND ({Visible})",conn);
        cmd.Parameters.AddWithValue("locale",locale); cmd.Parameters.AddWithValue("slug",slug);
        var id=await cmd.ExecuteScalarAsync(ct); return id is Guid guid? await GetAsync(guid,true,ct):null;
    }
    public async Task<BlogPost> SaveAsync(Guid? id,string locale,BlogSave item,CancellationToken ct)
    {
        await using var conn=await Open(ct); await using var tx=await conn.BeginTransactionAsync(ct);
        var postId=id??Guid.NewGuid();
        await using var allowed=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM cricastudio.blog_categories WHERE id=@category AND (@draft OR is_active)) AND EXISTS(SELECT 1 FROM cricastudio.catalog_languages WHERE code=@locale AND (@draft OR is_active))",conn,tx);
        allowed.Parameters.AddWithValue("category",item.CategoryId); allowed.Parameters.AddWithValue("locale",locale); allowed.Parameters.AddWithValue("draft",item.Version.Status is "draft" or "archived");
        if(!(bool)(await allowed.ExecuteScalarAsync(ct))!)throw new BlogValidationException("Categoria ou idioma inexistente/inativo para publicação.");
        var sql=id is null? "INSERT INTO cricastudio.blog_posts(id,category_id,cover_url,author,display_date,is_featured) VALUES(@id,@category,@cover,@author,@date,@featured)" :
            "UPDATE cricastudio.blog_posts SET category_id=@category,cover_url=@cover,author=@author,display_date=@date,is_featured=@featured,updated_at=CURRENT_TIMESTAMP WHERE id=@id";
        await using var metadata=new NpgsqlCommand(sql,conn,tx);
        metadata.Parameters.AddWithValue("id",postId); metadata.Parameters.AddWithValue("category",item.CategoryId);
        metadata.Parameters.AddWithValue("cover",item.Cover); metadata.Parameters.AddWithValue("author",item.Author);
        metadata.Parameters.AddWithValue("date",item.Date); metadata.Parameters.AddWithValue("featured",item.Featured);
        if(await metadata.ExecuteNonQueryAsync(ct)==0)throw new BlogValidationException("Matéria não encontrada.");
        var v=item.Version;
        await using var version=new NpgsqlCommand("""
            INSERT INTO cricastudio.blog_versions(post_id,language_code,title,summary,slug,document,seo_title,seo_description,status,publish_at,published_at)
            VALUES(@id,@locale,@title,@summary,@slug,@document,@seoTitle,@seoDescription,@status,@publishAt,CASE WHEN @status='published' THEN CURRENT_TIMESTAMP END)
            ON CONFLICT(post_id,language_code) DO UPDATE SET title=EXCLUDED.title,summary=EXCLUDED.summary,slug=EXCLUDED.slug,document=EXCLUDED.document,
              seo_title=EXCLUDED.seo_title,seo_description=EXCLUDED.seo_description,status=EXCLUDED.status,publish_at=EXCLUDED.publish_at,
              published_at=COALESCE(cricastudio.blog_versions.published_at,CASE WHEN EXCLUDED.status='published' THEN CURRENT_TIMESTAMP END),updated_at=CURRENT_TIMESTAMP
            """,conn,tx);
        version.Parameters.AddWithValue("id",postId);version.Parameters.AddWithValue("locale",locale);version.Parameters.AddWithValue("title",v.Title.Trim());
        version.Parameters.AddWithValue("summary",v.Summary.Trim());version.Parameters.AddWithValue("slug",v.Slug.Trim());version.Parameters.AddWithValue("document",NpgsqlDbType.Jsonb,v.Document.GetRawText());
        version.Parameters.AddWithValue("seoTitle",v.SeoTitle.Trim());version.Parameters.AddWithValue("seoDescription",v.SeoDescription.Trim());version.Parameters.AddWithValue("status",v.Status);
        Nullable(version,"publishAt",NpgsqlDbType.TimestampTz,v.Status=="scheduled"?v.PublishAt?.UtcDateTime:null);
        try { await version.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct); }
        catch(PostgresException ex) when(ex.SqlState==PostgresErrorCodes.UniqueViolation) {throw new BlogValidationException("Este slug já está em uso neste idioma.");}
        return (await GetAsync(postId,false,ct))!;
    }
    public async Task DeleteAsync(Guid id,CancellationToken ct) {await using var c=await Open(ct);await using var cmd=new NpgsqlCommand("DELETE FROM cricastudio.blog_posts WHERE id=@id",c);cmd.Parameters.AddWithValue("id",id);await cmd.ExecuteNonQueryAsync(ct);}
    public async Task<IReadOnlyList<BlogCategory>> CategoriesAsync(bool publicOnly,CancellationToken ct)
    {
        await using var c=await Open(ct);await using var cmd=new NpgsqlCommand("SELECT id,name,slug,is_active,translations FROM cricastudio.blog_categories WHERE NOT @public OR is_active ORDER BY name,id",c);cmd.Parameters.AddWithValue("public",publicOnly);
        var rows=new List<BlogCategory>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetBoolean(3),JsonSerializer.Deserialize<Dictionary<string,string>>(r.GetString(4),Json)!));return rows;
    }
    public async Task<BlogCategory> SaveCategoryAsync(BlogCategory item,CancellationToken ct)
    {
        var category=item with{Id=item.Id==Guid.Empty?Guid.NewGuid():item.Id};await using var c=await Open(ct);
        await using var cmd=new NpgsqlCommand("INSERT INTO cricastudio.blog_categories(id,name,slug,is_active,translations) VALUES(@id,@name,@slug,@active,@translations) ON CONFLICT(id) DO UPDATE SET name=EXCLUDED.name,slug=EXCLUDED.slug,is_active=EXCLUDED.is_active,translations=EXCLUDED.translations",c);
        cmd.Parameters.AddWithValue("id",category.Id);cmd.Parameters.AddWithValue("name",category.Name.Trim());cmd.Parameters.AddWithValue("slug",category.Slug);cmd.Parameters.AddWithValue("active",category.Active);cmd.Parameters.AddWithValue("translations",NpgsqlDbType.Jsonb,JsonSerializer.Serialize(category.Translations,Json));
        try{await cmd.ExecuteNonQueryAsync(ct);}catch(PostgresException ex)when(ex.SqlState==PostgresErrorCodes.UniqueViolation){throw new BlogValidationException("Nome ou slug de categoria já cadastrado.");}return category;
    }
    public async Task DeleteCategoryAsync(Guid id,CancellationToken ct)
    {
        await using var c=await Open(ct);await using var cmd=new NpgsqlCommand("DELETE FROM cricastudio.blog_categories WHERE id=@id",c);cmd.Parameters.AddWithValue("id",id);
        try{await cmd.ExecuteNonQueryAsync(ct);}catch(PostgresException ex)when(ex.SqlState==PostgresErrorCodes.ForeignKeyViolation){throw new BlogValidationException("Esta categoria está em uso. Desative-a para preservar as matérias.");}
    }
    private const string ProductSql="""
        SELECT p.id,'shop' kind,p.name,p.description,
          (SELECT i.image_url FROM cricastudio.shop_product_images i WHERE i.product_id=p.id ORDER BY i.sort_order LIMIT 1) image,
          '/loja/'||p.slug href,'Loja Crica' type FROM cricastudio.shop_products p WHERE p.status='published'
        UNION ALL
        SELECT p.id,'platform',COALESCE(t.fields->>'name',p.name),COALESCE(t.fields->>'description',p.description),p.logo_url,p.url,'Vitrine'
          FROM cricastudio.platforms p LEFT JOIN cricastudio.platform_translations t ON t.entity_id=p.id AND t.language_code=@locale AND t.status='reviewed'
          WHERE p.is_active AND p.status='published' AND p.url IS NOT NULL
        """;
    private static BlogProduct Product(NpgsqlDataReader r)=>new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),r.GetString(5),r.GetString(6));
    public async Task<BlogPage<BlogProduct>> ProductsAsync(string locale,string kind,string? query,int page,int pageSize,CancellationToken ct)
    {
        await using var c=await Open(ct);var where=$"FROM ({ProductSql}) products WHERE (@kind='' OR kind=@kind) AND name ILIKE @q";
        void Bind(NpgsqlCommand cmd){cmd.Parameters.AddWithValue("locale",locale);cmd.Parameters.AddWithValue("kind",kind);cmd.Parameters.AddWithValue("q","%"+(query??"").Replace("%","").Replace("_","")+"%");}
        await using var count=new NpgsqlCommand("SELECT count(*)::int "+where,c);Bind(count);var total=(int)(await count.ExecuteScalarAsync(ct))!;
        await using var cmd=new NpgsqlCommand("SELECT * "+where+" ORDER BY name,id LIMIT @limit OFFSET @offset",c);Bind(cmd);cmd.Parameters.AddWithValue("limit",pageSize);cmd.Parameters.AddWithValue("offset",(page-1)*pageSize);
        var rows=new List<BlogProduct>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(Product(r));return new(rows,total,page,pageSize);
    }
    public async Task<IReadOnlyList<BlogProduct>> ResolveProductsAsync(string locale,IReadOnlyList<Guid> shopIds,IReadOnlyList<Guid> platformIds,CancellationToken ct)
    {
        if(shopIds.Count+platformIds.Count==0)return [];
        await using var c=await Open(ct);await using var cmd=new NpgsqlCommand($"SELECT * FROM ({ProductSql}) products WHERE (kind='shop' AND id=ANY(@shop)) OR (kind='platform' AND id=ANY(@platform))",c);
        cmd.Parameters.AddWithValue("locale",locale);cmd.Parameters.AddWithValue("shop",NpgsqlDbType.Array|NpgsqlDbType.Uuid,shopIds.ToArray());cmd.Parameters.AddWithValue("platform",NpgsqlDbType.Array|NpgsqlDbType.Uuid,platformIds.ToArray());
        var rows=new List<BlogProduct>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(Product(r));return rows;
    }
    public async Task RecordImageAsync(Guid id,string url,Guid? userId,CancellationToken ct){await using var c=await Open(ct);await using var cmd=new NpgsqlCommand("INSERT INTO cricastudio.blog_images(id,url,uploaded_by) VALUES(@id,@url,@user)",c);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("url",url);Nullable(cmd,"user",NpgsqlDbType.Uuid,userId);await cmd.ExecuteNonQueryAsync(ct);}
    public async Task<bool> ImageExistsAsync(string url,CancellationToken ct){await using var c=await Open(ct);await using var cmd=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM cricastudio.blog_images WHERE url=@url)",c);cmd.Parameters.AddWithValue("url",url);return (bool)(await cmd.ExecuteScalarAsync(ct))!;}
    public async Task<IReadOnlyList<BlogPost>> SitemapAsync(CancellationToken ct)
    {
        await using var c=await Open(ct);await using var cmd=new NpgsqlCommand($"SELECT {Columns(true,false)} FROM cricastudio.blog_posts p JOIN cricastudio.blog_categories c ON c.id=p.category_id WHERE c.is_active AND EXISTS(SELECT 1 FROM cricastudio.blog_versions v JOIN cricastudio.catalog_languages l ON l.code=v.language_code WHERE v.post_id=p.id AND l.is_active AND ({Visible})) ORDER BY p.id",c);
        var rows=new List<BlogPost>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(Read(r));return rows;
    }
}
