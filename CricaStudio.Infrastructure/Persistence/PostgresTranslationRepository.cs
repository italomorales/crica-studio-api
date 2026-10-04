using System.Text.Json;
using System.Text.Json.Nodes;
using CricaStudio.Domain.Catalog;
using Npgsql;
namespace CricaStudio.Infrastructure.Persistence;

public sealed class PostgresTranslationRepository(string connectionString) : ITranslationRepository
{
    public async Task<bool> DeleteLanguageAsync(string code, CancellationToken ct) {
        if(code=="pt") throw new TranslationValidationException("O português é o idioma original e não pode ser excluído.");
        await using var c=new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("DELETE FROM cricastudio.catalog_languages WHERE code=@code",c);
        cmd.Parameters.AddWithValue("code",code);
        try { return await cmd.ExecuteNonQueryAsync(ct)>0; }
        catch(PostgresException ex) when(ex.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation) {
            throw new TranslationValidationException("Este idioma possui traduções vinculadas. Desative-o para preservar os textos ou remova suas traduções antes de excluir.");
        }
    }
    public async Task<IReadOnlyList<CatalogLanguage>> LanguagesAsync(bool publicOnly, CancellationToken ct) {
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT code,name,native_name,flag_code,is_active,sort_order FROM cricastudio.catalog_languages WHERE NOT @public OR is_active ORDER BY sort_order,code",c);
        command.Parameters.AddWithValue("public",publicOnly);
        await using var reader=await command.ExecuteReaderAsync(ct); var items=new List<CatalogLanguage>();
        while(await reader.ReadAsync(ct)) items.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3),reader.GetBoolean(4),reader.GetInt32(5)));
        return items;
    }
    public async Task<CatalogLanguage> SaveLanguageAsync(CatalogLanguage item, CancellationToken ct) {
        if (item.Code is null || item.Name is null || item.NativeName is null) throw new TranslationValidationException("Informe código e nomes do idioma.");
        if(!System.Text.RegularExpressions.Regex.IsMatch(item.Code,"^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$") || string.IsNullOrWhiteSpace(item.Name) || item.Name.Length>80 || string.IsNullOrWhiteSpace(item.NativeName) || item.NativeName.Length>80 || item.Order<0 || (item.Code=="pt" && !item.Active) || (item.FlagCode is not null && !System.Text.RegularExpressions.Regex.IsMatch(item.FlagCode,"^[A-Z]{2}$"))) throw new TranslationValidationException("Confira código, nomes, bandeira e ordem. Português deve permanecer ativo.");
        await using var c=new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var command=new NpgsqlCommand("INSERT INTO cricastudio.catalog_languages(code,name,native_name,flag_code,is_active,sort_order) VALUES(@code,@name,@native,@flag,@active,@order) ON CONFLICT(code) DO UPDATE SET name=EXCLUDED.name,native_name=EXCLUDED.native_name,flag_code=EXCLUDED.flag_code,is_active=EXCLUDED.is_active,sort_order=EXCLUDED.sort_order",c);
        command.Parameters.AddWithValue("code",item.Code); command.Parameters.AddWithValue("name",item.Name.Trim()); command.Parameters.AddWithValue("native",item.NativeName.Trim()); command.Parameters.AddWithValue("flag",(object?)item.FlagCode??DBNull.Value); command.Parameters.AddWithValue("active",item.Active); command.Parameters.AddWithValue("order",item.Order); await command.ExecuteNonQueryAsync(ct);
        return item;
    }
    public async Task<Dictionary<Guid,Dictionary<string,CatalogTranslation>>> ReadAsync(string resource,IReadOnlyList<Guid> ids,bool publicOnly,CancellationToken ct) {
        var rows=new Dictionary<Guid,Dictionary<string,CatalogTranslation>>(); if(ids.Count==0)return rows;
        var table=TranslationRules.Tables[resource];
        await using var c=new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand($"SELECT t.entity_id,t.language_code,t.status,t.fields::text FROM cricastudio.{table} t JOIN cricastudio.catalog_languages l ON l.code=t.language_code WHERE t.entity_id=ANY(@ids) AND (NOT @public OR (l.is_active AND t.status='reviewed'))",c);
        cmd.Parameters.AddWithValue("ids",ids.ToArray()); cmd.Parameters.AddWithValue("public",publicOnly);
        await using var r=await cmd.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct)) {
            var id=r.GetGuid(0); if(!rows.ContainsKey(id))rows[id]=[]; rows[id][r.GetString(1)]=new(r.GetString(2),JsonNode.Parse(r.GetString(3))!.AsObject());
        } return rows;
    }
    // Called inside the parent save transaction. null means preserve existing translations.
    public static async Task SaveAsync(NpgsqlConnection connection,NpgsqlTransaction? transaction,string resource,Guid id,Dictionary<string,CatalogTranslation>? translations,CancellationToken ct) {
        if(translations is null)return;
        TranslationRules.Validate(resource,translations); var table=TranslationRules.Tables[resource];
        await using(var verify=new NpgsqlCommand("SELECT code FROM cricastudio.catalog_languages WHERE code=ANY(@codes) FOR SHARE",connection,transaction)) {
            verify.Parameters.AddWithValue("codes",translations.Keys.ToArray()); var known=new HashSet<string>(); await using(var reader=await verify.ExecuteReaderAsync(ct)) while(await reader.ReadAsync(ct))known.Add(reader.GetString(0));
            if(translations.Keys.Any(code=>!known.Contains(code)))throw new TranslationValidationException("Cadastre o idioma antes de salvar suas traduções.");
        }
        await using(var remove=new NpgsqlCommand($"DELETE FROM cricastudio.{table} WHERE entity_id=@id",connection,transaction)){ remove.Parameters.AddWithValue("id",id); await remove.ExecuteNonQueryAsync(ct); }
        foreach(var (code,value) in translations) {
            await using var cmd=new NpgsqlCommand($"INSERT INTO cricastudio.{table}(entity_id,language_code,status,fields) VALUES(@id,@code,@status,@fields::jsonb)",connection,transaction);
            cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("code",code);cmd.Parameters.AddWithValue("status",value.Status);cmd.Parameters.AddWithValue("fields",value.Fields.ToJsonString());await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
