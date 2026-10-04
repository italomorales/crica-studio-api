using System.Text.Json;
using System.Text.Json.Nodes;
using CricaStudio.Domain.Catalog;
namespace CricaStudio.Api.Catalog;

public sealed class TranslationEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,EndpointFilterDelegate next)
    {
        var request=context.HttpContext.Request;
        var segments=request.Path.Value!.Split('/',StringSplitOptions.RemoveEmptyEntries);
        var catalogIndex=Array.IndexOf(segments,"catalog");
        var resource=catalogIndex>=0 && catalogIndex+1<segments.Length ? segments[catalogIndex+1] : null;
        if(resource is null || !TranslationRules.Tables.ContainsKey(resource))return await next(context);
        var repo=context.HttpContext.RequestServices.GetRequiredService<ITranslationRepository>();
        var ct=context.HttpContext.RequestAborted;
        var publicOnly=!segments.Contains("admin");
        var lang=request.Query["lang"].ToString();
        try {
            foreach(var argument in context.Arguments.OfType<ITranslationInput>()) TranslationRules.Validate(resource,argument.Translations);
            if(publicOnly && !string.IsNullOrEmpty(lang) && !(await repo.LanguagesAsync(true,ct)).Any(l=>l.Code==lang))
                return Results.ValidationProblem(new Dictionary<string,string[]> { ["lang"]=["Selecione um idioma ativo."] });
            var response=await next(context);
            if(request.Method!="GET")return response;
            if(response is IStatusCodeHttpResult status && status.StatusCode is >=400)return response;
            var value=response is IValueHttpResult http ? http.Value : response;
            if(value is null)return response;
            var json=JsonSerializer.SerializeToNode(value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var items=json is JsonArray array ? array.OfType<JsonObject>().ToArray() : json is JsonObject obj && obj["items"] is JsonArray page ? page.OfType<JsonObject>().ToArray() : json is JsonObject single ? [single] : Array.Empty<JsonObject>();
            var ids=items.Where(x=>Guid.TryParse(x["id"]?.ToString(),out _)).Select(x=>Guid.Parse(x["id"]!.ToString())).ToArray();
            var translations=await repo.ReadAsync(resource,ids,publicOnly,ct);
            var platformIds=resource=="affiliates" && publicOnly ? items.Where(x=>Guid.TryParse(x["platformId"]?.ToString(),out _)).Select(x=>Guid.Parse(x["platformId"]!.ToString())).Distinct().ToArray() : [];
            var platformTranslations=await repo.ReadAsync("platforms",platformIds,true,ct);
            foreach(var item in items) {
                if(!Guid.TryParse(item["id"]?.ToString(),out var id))continue;
                var map=translations.GetValueOrDefault(id)??[];
                item["translations"]=JsonSerializer.SerializeToNode(map,new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if(publicOnly) {
                    if(resource=="affiliates" && Guid.TryParse(item["platformId"]?.ToString(),out var platformId)) {
                        item["platformContent"]=new JsonObject {
                            ["name"]=item["platform"]?.DeepClone(),
                            ["translations"]=JsonSerializer.SerializeToNode(platformTranslations.GetValueOrDefault(platformId)??[],new JsonSerializerOptions(JsonSerializerDefaults.Web))
                        };
                    }
                    var originals=new JsonObject();
                    foreach(var field in new[]{"name","description","fullDescription","characteristics","personalization"})
                        if(item.ContainsKey(field)) { originals[field]=item[field]?.DeepClone(); var translated=TranslationRules.Field(map.GetValueOrDefault(lang),field); if(translated is not null)item[field]=translated.DeepClone(); }
                    item["original"]=originals;
                }
            }
            return Results.Json(json);
        } catch(TranslationValidationException ex) { return Results.ValidationProblem(new Dictionary<string,string[]> { ["translations"]=[ex.Message] }); }
    }
}
public static class TranslationEndpoints
{
    public static void MapTranslationEndpoints(this WebApplication app) {
        app.MapGet("/api/catalog/languages",(ITranslationRepository repo,CancellationToken ct)=>repo.LanguagesAsync(true,ct));
        var admin=app.MapGroup("/api/admin/catalog/languages").RequireAuthorization();
        admin.MapDelete("/{code}",async(string code,ITranslationRepository repo,CancellationToken ct)=> {
            try { return await repo.DeleteLanguageAsync(code,ct) ? Results.NoContent() : Results.NotFound(); }
            catch(TranslationValidationException ex) { return Results.ValidationProblem(new Dictionary<string,string[]> { ["language"]=[ex.Message] }); }
        });
        admin.MapGet("/",(ITranslationRepository repo,CancellationToken ct)=>repo.LanguagesAsync(false,ct));
        admin.MapPost("/",async(CatalogLanguage item,ITranslationRepository repo,CancellationToken ct)=> {
            if((await repo.LanguagesAsync(false,ct)).Any(l=>l.Code==item.Code))return Results.ValidationProblem(new Dictionary<string,string[]> { ["code"]=["Esse idioma já está cadastrado."] });
            return await Save(item,repo,ct);
        });
        admin.MapPut("/{code}",async(string code,CatalogLanguage item,ITranslationRepository repo,CancellationToken ct)=> {
            if(!(await repo.LanguagesAsync(false,ct)).Any(l=>l.Code==code))return Results.NotFound();
            return await Save(item with{Code=code},repo,ct);
        });
    }
    private static async Task<IResult> Save(CatalogLanguage item,ITranslationRepository repo,CancellationToken ct) {
        try{return Results.Ok(await repo.SaveLanguageAsync(item,ct));}catch(TranslationValidationException ex){return Results.ValidationProblem(new Dictionary<string,string[]> { ["language"]=[ex.Message] });}
    }
}
