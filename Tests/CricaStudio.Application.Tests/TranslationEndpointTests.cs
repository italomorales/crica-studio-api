using System.Text.Json.Nodes;
using CricaStudio.Api.Catalog;
using CricaStudio.Domain.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class TranslationEndpointTests
{
    [Theory]
    [InlineData("/api/catalog/products")]
    [InlineData("/api/catalog/products/types")]
    [InlineData("/api/admin/catalog/products")]
    public async Task Shop_products_ignore_language_and_legacy_translations(string path)
    {
        var id=Guid.NewGuid();var repo=new MemoryTranslations(id);var http=Context(repo,path,"?lang=en");
        var response=Results.Ok(new {id,name="Caneca original",description="Descrição original",characteristics=new[]{"Cerâmica"},price=65,slug="caneca-original"});
        var result=await new TranslationEndpointFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(http),_=>ValueTask.FromResult<object?>(response));
        Assert.Same(response,result);
        Assert.Equal(0,repo.ReadCalls);
        Assert.False(typeof(ITranslationInput).IsAssignableFrom(typeof(ProductRequest)));
        Assert.False(typeof(ITranslationInput).IsAssignableFrom(typeof(ShopProduct)));
    }
    [Fact]
    public async Task Public_page_localizes_reviewed_fields_retains_original_and_does_not_leak_drafts()
    {
        var id=Guid.NewGuid();var repo=new MemoryTranslations(id);
        var http=Context(repo,"/api/catalog/affiliates","?lang=en&market=br");
        var result=await new TranslationEndpointFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(http),_=>ValueTask.FromResult<object?>(Results.Ok(new { items=new[]{new {id,name="Caneca",description="Português",price=30,slug="caneca"}},total=1,page=1 })));
        var json=Assert.IsType<JsonObject>(((IValueHttpResult)result!).Value);
        var item=json["items"]![0]!;
        Assert.Equal("Mug",item["name"]!.GetValue<string>());Assert.Equal("Português",item["description"]!.GetValue<string>());
        Assert.Equal("Caneca",item["original"]!["name"]!.GetValue<string>());
        Assert.Equal(30,item["price"]!.GetValue<int>());Assert.Equal("caneca",item["slug"]!.GetValue<string>());
        Assert.Null(item["translations"]!["es"]);Assert.Equal(1,json["total"]!.GetValue<int>());
        Assert.Equal("?lang=en&market=br",http.Request.QueryString.Value);
    }

    [Fact]
    public async Task Admin_read_keeps_portuguese_and_exposes_drafts_for_editing()
    {
        var id=Guid.NewGuid();var http=Context(new MemoryTranslations(id),"/api/admin/catalog/types","?lang=en");
        var result=await new TranslationEndpointFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(http),_=>ValueTask.FromResult<object?>(Results.Ok(new {id,name="Caneca"})));
        var item=Assert.IsType<JsonObject>(((IValueHttpResult)result!).Value);
        Assert.Equal("Caneca",item["name"]!.GetValue<string>());Assert.Equal("draft",item["translations"]!["es"]!["status"]!.GetValue<string>());Assert.Null(item["original"]);
    }

    [Fact]
    public async Task Inactive_or_unknown_language_rejects_request_before_handler_runs()
    {
        var http=Context(new MemoryTranslations(Guid.NewGuid()),"/api/catalog/types","?lang=fr");var called=false;
        var result=await new TranslationEndpointFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(http),_=>{called=true;return ValueTask.FromResult<object?>(Results.Ok());});
        Assert.False(called);Assert.Equal(400,((IStatusCodeHttpResult)result!).StatusCode);
    }

    private static DefaultHttpContext Context(ITranslationRepository repo,string path,string query)
    {
        var http=new DefaultHttpContext();http.RequestServices=new ServiceCollection().AddSingleton(repo).BuildServiceProvider();http.Request.Method="GET";http.Request.Path=path;http.Request.QueryString=new(query);return http;
    }
    private sealed class MemoryTranslations(Guid id):ITranslationRepository
    {
        public int ReadCalls { get; private set; }
        public Task<bool> DeleteLanguageAsync(string code,CancellationToken ct)=>Task.FromResult(false);
        public Task<IReadOnlyList<CatalogLanguage>> LanguagesAsync(bool publicOnly,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CatalogLanguage>>([new("pt","Português","Português","BR",true,0),new("en","Inglês","English","US",true,10),new("es","Espanhol","Español","ES",true,20)]);
        public Task<CatalogLanguage> SaveLanguageAsync(CatalogLanguage item,CancellationToken ct)=>Task.FromResult(item);
        public Task<Dictionary<Guid,Dictionary<string,CatalogTranslation>>> ReadAsync(string resource,IReadOnlyList<Guid> ids,bool publicOnly,CancellationToken ct)
        {
            ReadCalls++;
            if(!ids.Contains(id))return Task.FromResult(new Dictionary<Guid,Dictionary<string,CatalogTranslation>>());
            var map=new Dictionary<string,CatalogTranslation>{["en"]=new("reviewed",new(){["name"]="Mug",["description"]=" "})};if(!publicOnly)map["es"]=new("draft",new(){["name"]="Taza"});
            return Task.FromResult(new Dictionary<Guid,Dictionary<string,CatalogTranslation>>{[id]=map});
        }
    }
}
