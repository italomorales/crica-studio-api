using System.Text.Json.Nodes;
using System.Transactions;
using CricaStudio.Domain.Catalog;
using CricaStudio.Infrastructure.Persistence;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class CatalogTranslationTests
{
    [PlatformDatabaseFact]
    public async Task Language_deletion_removes_unused_codes_but_preserves_original_and_linked_translations()
    {
        var cs=Environment.GetEnvironmentVariable("CRICA_PLATFORMS_TEST_DATABASE")!;var repo=new PostgresTranslationRepository(cs);var write=new PostgresCatalogWriteRepository(cs);var ct=CancellationToken.None;
        await Assert.ThrowsAsync<TranslationValidationException>(()=>repo.DeleteLanguageAsync("pt",ct));
        Assert.False(await repo.DeleteLanguageAsync("not-registered",ct));
        using(var scope=new TransactionScope(TransactionScopeAsyncFlowOption.Enabled)) {
            var language=new CatalogLanguage("zzq","QA disposable","QA",null,false,99);
            await repo.SaveLanguageAsync(language,ct);Assert.True(await repo.DeleteLanguageAsync(language.Code,ct));
            Assert.DoesNotContain(await repo.LanguagesAsync(false,ct),l=>l.Code==language.Code);
            await repo.SaveLanguageAsync(language,ct);
            await write.SaveTypeAsync(new(Guid.Empty,"Delete QA "+Guid.NewGuid(),"both",true){Translations=new(){[language.Code]=new("draft",new(){["name"]="QA"})}},ct);
            await Assert.ThrowsAsync<TranslationValidationException>(()=>repo.DeleteLanguageAsync(language.Code,ct));
        }
        Assert.DoesNotContain(await repo.LanguagesAsync(false,ct),l=>l.Code=="zzq");
    }
    private static Dictionary<string,CatalogTranslation> Translations() => new() {
        ["en"] = new("reviewed",new JsonObject { ["name"]="Translation QA unique name" }),
        ["es"] = new("draft",new JsonObject { ["name"]="Nombre sin revisar" })
    };

    [Fact]
    public void Field_falls_back_for_drafts_blank_strings_and_empty_lists()
    {
        Assert.Null(TranslationRules.Field(new("draft",new(){["name"]="Hidden"}),"name"));
        Assert.Null(TranslationRules.Field(new("reviewed",new(){["name"]="  "}),"name"));
        Assert.Null(TranslationRules.Field(new("reviewed",new(){["characteristics"]=new JsonArray(" ")}),"characteristics"));
        Assert.Equal("Mug",TranslationRules.Field(new("reviewed",new(){["name"]="Mug"}),"name")!.GetValue<string>());
    }

    [Theory]
    [InlineData("types","price")]
    [InlineData("themes","description")]
    [InlineData("products","slug")]
    [InlineData("affiliates","affiliateUrl")]
    [InlineData("platforms","locale")]
    public void Translations_cannot_change_identifiers_links_prices_or_market(string resource,string field)
    {
        Assert.Throws<TranslationValidationException>(()=>TranslationRules.Validate(resource,new(){["en"]=new("reviewed",new(){[field]="invalid"})}));
    }

    [Fact]
    public void Original_language_statuses_and_field_types_are_validated()
    {
        Assert.Throws<TranslationValidationException>(()=>TranslationRules.Validate("types",new(){["pt"]=new("reviewed",new())}));
        Assert.Throws<TranslationValidationException>(()=>TranslationRules.Validate("types",new(){["en"]=new("published",new())}));
        Assert.Throws<TranslationValidationException>(()=>TranslationRules.Validate("products",new(){["en"]=new("reviewed",new(){["characteristics"]="not a list"})}));
        Assert.Throws<TranslationValidationException>(()=>TranslationRules.Validate("products",new(){["en"]=new("reviewed",new(){["name"]="Mug"})}));
    }

    [PlatformDatabaseFact]
    public async Task Supported_catalog_entities_persist_translations_while_shop_products_keep_originals()
    {
        var cs=Environment.GetEnvironmentVariable("CRICA_PLATFORMS_TEST_DATABASE")!;
        var write=new PostgresCatalogWriteRepository(cs);var read=new PostgresCatalogReadRepository(cs);
        var repo=new PostgresTranslationRepository(cs);var platforms=new PostgresPlatformRepository(cs);var ct=CancellationToken.None;
        var typeId=Guid.NewGuid();
        using(var scope=new TransactionScope(TransactionScopeAsyncFlowOption.Enabled)) {
            var future=new CatalogLanguage("fr","Francês","Français","FR",false,30);
            await repo.SaveLanguageAsync(future,ct);
            Assert.DoesNotContain(await repo.LanguagesAsync(true,ct),l=>l.Code=="fr");
            await repo.SaveLanguageAsync(future with{Active=true},ct);
            Assert.Equal("Français",(await repo.LanguagesAsync(true,ct)).Single(l=>l.Code=="fr").NativeName);
            var type=await write.SaveTypeAsync(new(typeId,"QA "+typeId.ToString("N"),"both",true){Translations=Translations()},ct);
            var theme=await write.SaveThemeAsync(new(Guid.Empty,"QA theme "+typeId,true){Translations=Translations()},ct);
            var platform=new CatalogPlatform(Guid.NewGuid(),"QA platform","qa-"+typeId.ToString("N"),"Descrição", "https://example.com/?ref=qa",null,"en-US","US","published",99,false,true){Translations=Translations()};
            await platforms.SaveAsync(platform,true,ct);
            var shop=await write.SaveShopProductAsync(new(Guid.Empty,type.Id,"Produto original","qa-"+typeId,"Descrição original","Texto longo","fixed",25,true,false,["Original"],["Nome"],[],"published",99){ThemeIds=[theme.Id]},ct);
            var affiliate=await write.SaveAffiliateProductAsync(new(Guid.Empty,type.Id,"Indicação original","Descrição",platform.Name,null,"https://example.com/?ref=qa",null,true,false,"published",99){PlatformId=platform.Id,Translations=Translations()},ct);
            foreach(var (resource,id) in new[]{("types",type.Id),("themes",theme.Id),("affiliates",affiliate.Id),("platforms",platform.Id)}) {
                var admin=await repo.ReadAsync(resource,[id],false,ct);Assert.Equal(2,admin[id].Count);
                var publicRows=await repo.ReadAsync(resource,[id],true,ct);Assert.Single(publicRows[id]);Assert.Equal("reviewed",publicRows[id]["en"].Status);
            }
            var stored=(await read.GetAllShopProductsAsync(ct)).Single(p=>p.Id==shop.Id);
            Assert.Equal("Produto original",stored.Name);Assert.Equal(25,stored.Price);Assert.Equal(shop.Slug,stored.Slug);Assert.Equal(shop.ThemeIds,stored.ThemeIds);
            var search=await read.GetPublishedShopProductPageAsync(1,12,"translation qa unique name",type.Id,false,ct);
            Assert.Equal(0,search.Total);Assert.Empty(search.Items);
            var originalSearch=await read.GetPublishedShopProductPageAsync(1,12,"produto original",type.Id,false,ct);
            Assert.Equal(shop.Id,Assert.Single(originalSearch.Items).Id);
            await write.SaveTypeAsync(type with{Name="Português atualizado",Translations=null},ct);
            Assert.Equal(2,(await repo.ReadAsync("types",[type.Id],false,ct))[type.Id].Count);
            var en=(await repo.LanguagesAsync(false,ct)).Single(l=>l.Code=="en");
            await repo.SaveLanguageAsync(en with{Active=false},ct);
            Assert.Empty(await repo.ReadAsync("types",[type.Id],true,ct));Assert.Equal(2,(await repo.ReadAsync("types",[type.Id],false,ct))[type.Id].Count);
            await write.SaveTypeAsync(type with{Translations=[]},ct);Assert.Empty(await repo.ReadAsync("types",[type.Id],false,ct));
        }
        Assert.DoesNotContain(await read.GetAllTypesAsync(ct),t=>t.Id==typeId);
    }

    [PlatformDatabaseFact]
    public async Task Unknown_language_rolls_back_parent_save_and_original_portuguese_cannot_be_deactivated()
    {
        var cs=Environment.GetEnvironmentVariable("CRICA_PLATFORMS_TEST_DATABASE")!;var write=new PostgresCatalogWriteRepository(cs);var read=new PostgresCatalogReadRepository(cs);var repo=new PostgresTranslationRepository(cs);var ct=CancellationToken.None;
        var id=Guid.NewGuid();
        await Assert.ThrowsAsync<TranslationValidationException>(()=>write.SaveTypeAsync(new(id,"Rollback "+id,"both",true){Translations=new(){["zz"]=new("reviewed",new(){["name"]="Unknown"})}},ct));
        Assert.DoesNotContain(await read.GetAllTypesAsync(ct),t=>t.Id==id);
        var pt=(await repo.LanguagesAsync(false,ct)).Single(l=>l.Code=="pt");
        await Assert.ThrowsAsync<TranslationValidationException>(()=>repo.SaveLanguageAsync(pt with{Active=false},ct));
        await Assert.ThrowsAsync<TranslationValidationException>(()=>repo.SaveLanguageAsync(pt with{Code="invalid_code"},ct));
    }
}
