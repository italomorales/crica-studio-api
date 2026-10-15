using CricaStudio.Domain.Blog;
using Npgsql;
using NpgsqlTypes;

namespace CricaStudio.Infrastructure.Persistence;

public sealed class PostgresBlogActivityRepository(string connectionString)
{
    private async Task<NpgsqlConnection> Open(CancellationToken ct){var c=new NpgsqlConnection(connectionString);await c.OpenAsync(ct);return c;}
    private static void Nullable(NpgsqlCommand cmd,string key,NpgsqlDbType type,object? value)=>cmd.Parameters.Add(key,type).Value=value??DBNull.Value;
    public async Task<BlogVisitor> Visitor(string provider,string subject,string name,CancellationToken ct)
    {
        if(subject.Length is 0 or >255)throw new BlogValidationException("Identidade inválida.");
        name=string.IsNullOrWhiteSpace(name)?"Visitante":name.Trim();if(name.Length>120)name=name[..120];
        await using var conn=await Open(ct);
        await using var cmd=new NpgsqlCommand("INSERT INTO cricastudio.blog_visitors(id,provider,subject,name) VALUES(@id,@provider,@subject,@name) ON CONFLICT(provider,subject) DO UPDATE SET name=EXCLUDED.name RETURNING id,name,provider",conn);
        cmd.Parameters.AddWithValue("id",Guid.NewGuid());cmd.Parameters.AddWithValue("provider",provider);cmd.Parameters.AddWithValue("subject",subject);cmd.Parameters.AddWithValue("name",name);
        await using var r=await cmd.ExecuteReaderAsync(ct);await r.ReadAsync(ct);return new(r.GetGuid(0),r.GetString(1),r.GetString(2));
    }
    // Only approved roots and approved replies whose entire ancestor chain is approved are public.
    private const string PublicTree="""
      WITH RECURSIVE visible AS (
       SELECT a.id FROM cricastudio.blog_activity a WHERE a.post_id=@post AND a.language_code=@locale AND a.kind='comment' AND a.status='approved' AND a.parent_id IS NULL
       UNION ALL SELECT a.id FROM cricastudio.blog_activity a JOIN visible v ON a.parent_id=v.id WHERE a.status='approved' AND a.kind='comment' AND a.post_id=@post AND a.language_code=@locale
      )
      """;
    public async Task<BlogEngagement> Engagement(Guid post,string locale,Guid? visitor,CancellationToken ct)
    {
        await using var conn=await Open(ct);
        await using var cmd=new NpgsqlCommand(PublicTree+" SELECT (SELECT count(*)::int FROM cricastudio.blog_activity WHERE post_id=@post AND language_code=@locale AND kind='like' AND status='approved'),(SELECT count(*)::int FROM visible),(SELECT status FROM cricastudio.blog_activity WHERE post_id=@post AND language_code=@locale AND visitor_id=@visitor AND kind='like')",conn);
        cmd.Parameters.AddWithValue("post",post);cmd.Parameters.AddWithValue("locale",locale);Nullable(cmd,"visitor",NpgsqlDbType.Uuid,visitor);
        await using var r=await cmd.ExecuteReaderAsync(ct);await r.ReadAsync(ct);return new(r.GetInt32(0),r.GetInt32(1),r.IsDBNull(2)?null:r.GetString(2));
    }
    private static BlogActivity Read(NpgsqlDataReader r)=>new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetGuid(3),r.GetString(4),r.GetString(5),r.GetString(6),r.IsDBNull(7)?null:r.GetGuid(7),r.GetString(8),r.GetFieldValue<DateTimeOffset>(9),r.GetString(10),r.IsDBNull(11)?null:r.GetString(11));
    private const string Columns="a.id,a.post_id,a.language_code,a.visitor_id,u.name,a.kind,a.text,a.parent_id,a.status,a.created_at,COALESCE(v.title,''),parent.text";
    private const string Joins=" FROM cricastudio.blog_activity a JOIN cricastudio.blog_visitors u ON u.id=a.visitor_id LEFT JOIN cricastudio.blog_versions v ON v.post_id=a.post_id AND v.language_code=a.language_code LEFT JOIN cricastudio.blog_activity parent ON parent.id=a.parent_id ";
    public async Task<BlogPage<BlogActivity>> Comments(Guid post,string locale,int page,Guid? visitor,CancellationToken ct)
    {
        await using var conn=await Open(ct);
        // Pending roots belonging to this visitor may be shown privately. Replies wait for their approved parent.
        var where=" WHERE a.post_id=@post AND a.language_code=@locale AND a.kind='comment' AND (a.id IN(SELECT id FROM visible) OR (a.visitor_id=@visitor AND a.status='pending' AND (a.parent_id IS NULL OR a.parent_id IN(SELECT id FROM visible))))";
        void Bind(NpgsqlCommand c){c.Parameters.AddWithValue("post",post);c.Parameters.AddWithValue("locale",locale);Nullable(c,"visitor",NpgsqlDbType.Uuid,visitor);}
        await using var count=new NpgsqlCommand(PublicTree+" SELECT count(*)::int "+Joins+where,conn);Bind(count);var total=(int)(await count.ExecuteScalarAsync(ct))!;
        await using var cmd=new NpgsqlCommand(PublicTree+" SELECT "+Columns+Joins+where+" ORDER BY a.created_at,a.id LIMIT 20 OFFSET @offset",conn);Bind(cmd);cmd.Parameters.AddWithValue("offset",(page-1)*20);
        var rows=new List<BlogActivity>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(Read(r));return new(rows,total,page,20);
    }
    public async Task<BlogActivity> Add(Guid post,string locale,Guid visitor,string kind,string? text,Guid? parent,CancellationToken ct)
    {
        text=text?.Trim()??"";
        if(kind is not ("comment" or "like" or "share")||text.Length>1500||(kind=="comment"&&text.Length==0)||(kind!="comment"&&parent is not null))throw new BlogValidationException("Interação inválida. Escreva até 1500 caracteres.");
        await using var conn=await Open(ct);
        // Serialize submissions per visitor, including across API instances, to enforce cooldowns.
        await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT id FROM cricastudio.blog_visitors WHERE id=@visitor FOR UPDATE",conn,tx)){gate.Parameters.AddWithValue("visitor",visitor);if(await gate.ExecuteScalarAsync(ct) is null)throw new BlogValidationException("Entre novamente para continuar.");}
        if(kind=="comment"||kind=="share"){
            await using var rate=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM cricastudio.blog_activity WHERE visitor_id=@visitor AND kind=@kind AND created_at>current_timestamp-interval '15 seconds')",conn,tx);rate.Parameters.AddWithValue("visitor",visitor);rate.Parameters.AddWithValue("kind",kind);
            if((bool)(await rate.ExecuteScalarAsync(ct))!)throw new BlogValidationException("Aguarde alguns segundos antes de enviar novamente.");
        }
        if(parent is not null){
            await using var check=new NpgsqlCommand(PublicTree+" SELECT EXISTS(SELECT 1 FROM visible WHERE id=@parent)",conn,tx);check.Parameters.AddWithValue("post",post);check.Parameters.AddWithValue("locale",locale);check.Parameters.AddWithValue("parent",parent);
            if(!(bool)(await check.ExecuteScalarAsync(ct))!)throw new BlogValidationException("Responda a um comentário aprovado desta matéria e idioma.");
        }
        var id=Guid.NewGuid();
        await using(var insert=new NpgsqlCommand("INSERT INTO cricastudio.blog_activity(id,post_id,language_code,visitor_id,kind,text,parent_id,status) VALUES(@id,@post,@locale,@visitor,@kind,@text,@parent,@status) ON CONFLICT(post_id,language_code,visitor_id) WHERE kind='like' DO NOTHING",conn,tx)){
            insert.Parameters.AddWithValue("id",id);insert.Parameters.AddWithValue("post",post);insert.Parameters.AddWithValue("locale",locale);insert.Parameters.AddWithValue("visitor",visitor);insert.Parameters.AddWithValue("kind",kind);insert.Parameters.AddWithValue("text",text);insert.Parameters.AddWithValue("status",kind=="comment"?"pending":"approved");Nullable(insert,"parent",NpgsqlDbType.Uuid,parent);await insert.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        await using var result=new NpgsqlCommand("SELECT "+Columns+Joins+" WHERE a.id=@id OR (a.post_id=@post AND a.language_code=@locale AND a.visitor_id=@visitor AND a.kind='like' AND @kind='like') LIMIT 1",conn);
        result.Parameters.AddWithValue("id",id);result.Parameters.AddWithValue("post",post);result.Parameters.AddWithValue("locale",locale);result.Parameters.AddWithValue("visitor",visitor);result.Parameters.AddWithValue("kind",kind);
        await using var r=await result.ExecuteReaderAsync(ct);await r.ReadAsync(ct);return Read(r);
    }
    public async Task Unlike(Guid post,string locale,Guid visitor,CancellationToken ct){await using var conn=await Open(ct);await using var cmd=new NpgsqlCommand("DELETE FROM cricastudio.blog_activity WHERE post_id=@post AND language_code=@locale AND visitor_id=@visitor AND kind='like'",conn);cmd.Parameters.AddWithValue("post",post);cmd.Parameters.AddWithValue("locale",locale);cmd.Parameters.AddWithValue("visitor",visitor);await cmd.ExecuteNonQueryAsync(ct);}
    public async Task<BlogPage<BlogActivity>> Moderation(string status,string kind,int page,int pageSize,CancellationToken ct)
    {
        await using var conn=await Open(ct);var where=" WHERE a.kind='comment' AND (@status='' OR a.status=@status) AND (@kind='' OR a.kind=@kind)";
        void Bind(NpgsqlCommand c){c.Parameters.AddWithValue("status",status);c.Parameters.AddWithValue("kind",kind);}
        await using var count=new NpgsqlCommand("SELECT count(*)::int FROM cricastudio.blog_activity a"+where,conn);Bind(count);var total=(int)(await count.ExecuteScalarAsync(ct))!;
        await using var cmd=new NpgsqlCommand("SELECT "+Columns+Joins+where+" ORDER BY a.created_at DESC,a.id LIMIT @limit OFFSET @offset",conn);Bind(cmd);cmd.Parameters.AddWithValue("limit",pageSize);cmd.Parameters.AddWithValue("offset",(page-1)*pageSize);
        var rows=new List<BlogActivity>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(Read(r));return new(rows,total,page,pageSize);
    }
    public async Task<bool> Moderate(Guid id,string status,Guid? admin,CancellationToken ct)
    {
        if(status is not ("pending" or "approved" or "rejected" or "spam"))throw new BlogValidationException("Situação inválida.");
        await using var conn=await Open(ct);await using var cmd=new NpgsqlCommand("UPDATE cricastudio.blog_activity SET status=@status,moderated_at=current_timestamp,moderated_by=@admin WHERE id=@id AND kind='comment'",conn);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("status",status);Nullable(cmd,"admin",NpgsqlDbType.Uuid,admin);return await cmd.ExecuteNonQueryAsync(ct)>0;
    }
}
