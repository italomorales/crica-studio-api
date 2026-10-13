using System.Security.Claims;
using System.Text.RegularExpressions;
using Amazon.S3;
using CricaStudio.Api.Catalog;
using CricaStudio.Application.Blog;
using CricaStudio.Domain.Blog;

namespace CricaStudio.Api.Blog;

public sealed record BlogWriteRequest(Guid CategoryId,string Cover,string Author,DateOnly Date,bool Featured,BlogVersion Version);
public sealed class BlogEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,EndpointFilterDelegate next)
    {
        try{return await next(context);}
        catch(BlogValidationException ex){return Results.ValidationProblem(new Dictionary<string,string[]>{{"blog",[ex.Message]}});}
        catch(CatalogMediaValidationException ex){return Results.ValidationProblem(new Dictionary<string,string[]>{{"file",[ex.Message]}});}
        catch(System.Text.Json.JsonException){return Results.ValidationProblem(new Dictionary<string,string[]>{{"content",["Conteúdo JSON inválido."]}});}
        catch(Npgsql.NpgsqlException){return Results.Problem(title:"Não foi possível acessar o blog no banco de dados.",statusCode:503);}
        catch(AmazonS3Exception){return Results.Problem(title:"Não foi possível enviar a imagem ao armazenamento.",statusCode:503);}
    }
}
public static class BlogEndpoints
{
    private static BlogSave Save(BlogWriteRequest request)=>new(request.CategoryId,request.Cover,request.Author,request.Date,request.Featured,request.Version);
    private static void Locale(string locale){if(!BlogPolicy.ValidLocale(locale))throw new BlogValidationException("Idioma inválido.");}
    public static void MapBlogEndpoints(this WebApplication app)
    {
        var admin=app.MapGroup("/api/admin/blog").RequireAuthorization().WithTags("Blog admin").AddEndpointFilter<BlogEndpointFilter>();
        var blog=app.MapGroup("/api/blog").WithTags("Blog").AddEndpointFilter<BlogEndpointFilter>();
        admin.MapGet("/posts",async(string? locale,int? page,int? pageSize,string? query,Guid? categoryId,string? status,IBlogRepository repo,BlogService service,CancellationToken ct)=>{
            var lang=locale??"pt";Locale(lang);var data=await repo.ListAsync(lang,Math.Clamp(page??1,1,100000),Math.Clamp(pageSize??20,1,100),query,categoryId,status,false,ct);
            var rows=new List<object>();foreach(var post in data.Items)rows.Add(await service.DtoAsync(post,false,false,ct));return Results.Ok(new{items=rows,data.Total,data.Page,data.PageSize});
        });
        admin.MapGet("/posts/{id:guid}",async(Guid id,IBlogRepository repo,BlogService service,CancellationToken ct)=>{
            var post=await repo.GetAsync(id,false,ct);return post is null?Results.NotFound():Results.Ok(await service.DtoAsync(post,true,false,ct));
        });
        admin.MapPost("/posts",async(string locale,BlogWriteRequest request,BlogService service,CancellationToken ct)=>{
            Locale(locale);var post=await service.SaveAsync(null,locale,Save(request),ct);return Results.Created($"/api/admin/blog/posts/{post.Id}",await service.DtoAsync(post,true,false,ct));
        });
        admin.MapPut("/posts/{id:guid}/versions/{locale}",async(Guid id,string locale,BlogWriteRequest request,IBlogRepository repo,BlogService service,CancellationToken ct)=>{
            Locale(locale);if(await repo.GetAsync(id,false,ct) is null)return Results.NotFound();var post=await service.SaveAsync(id,locale,Save(request),ct);return Results.Ok(await service.DtoAsync(post,true,false,ct));
        });
        admin.MapDelete("/posts/{id:guid}",async(Guid id,IBlogRepository repo,CancellationToken ct)=>{await repo.DeleteAsync(id,ct);return Results.NoContent();});
        admin.MapPost("/preview",async(string locale,BlogWriteRequest request,BlogService service,CancellationToken ct)=>{
            Locale(locale);var item=Save(request);BlogPolicy.Validate(item with{Version=item.Version with{Status="draft"}},locale,DateTimeOffset.UtcNow);
            var content=await service.ContentAsync(item.Version,locale,false,ct);
            return Results.Ok(new{html=content.Html,products=content.Products});
        });
        admin.MapGet("/categories",async(IBlogRepository repo,CancellationToken ct)=>Results.Ok(await repo.CategoriesAsync(false,ct)));
        admin.MapPut("/categories/{id:guid}",async(Guid id,BlogCategory category,IBlogRepository repo,CancellationToken ct)=>{
            if(string.IsNullOrWhiteSpace(category.Name)||category.Name.Length>120||category.Slug is null||!Regex.IsMatch(category.Slug,"^[a-z0-9]+(?:-[a-z0-9]+)*$")||category.Slug.Length>140||category.Translations is null||category.Translations.Count>100||category.Translations.Any(p=>!BlogPolicy.ValidLocale(p.Key)||p.Value is null||p.Value.Length>120))throw new BlogValidationException("Nome, slug ou traduções de categoria inválidos.");
            return Results.Ok(await repo.SaveCategoryAsync(category with{Id=id},ct));
        });
        admin.MapDelete("/categories/{id:guid}",async(Guid id,IBlogRepository repo,CancellationToken ct)=>{await repo.DeleteCategoryAsync(id,ct);return Results.NoContent();});
        admin.MapGet("/products",async(string? locale,string? kind,string? query,int? page,int? pageSize,IBlogRepository repo,CancellationToken ct)=>{
            if(kind is not(null or "" or "shop" or "platform"))throw new BlogValidationException("Tipo de produto inválido.");return Results.Ok(await repo.ProductsAsync(locale??"pt",kind??"",query,Math.Clamp(page??1,1,100000),Math.Clamp(pageSize??20,1,50),ct));
        });
        admin.MapPost("/images",async(IFormFile file,ICatalogMediaStorage storage,IBlogRepository repo,ClaimsPrincipal user,CancellationToken ct)=>{
            var url=await storage.UploadAsync("blog",file,ct);Guid? userId=Guid.TryParse(user.FindFirstValue("sub"),out var id)?id:null;
            await repo.RecordImageAsync(Guid.NewGuid(),url,userId,ct);return Results.Ok(new{url});
        }).DisableAntiforgery();
        blog.MapGet("/categories",async(string? locale,IBlogRepository repo,CancellationToken ct)=>Results.Ok((await repo.CategoriesAsync(true,ct)).Select(c=>c with{Name=c.Translations.GetValueOrDefault(locale??"pt",c.Name)})));
        blog.MapGet("/posts",async(string? locale,int? page,int? pageSize,string? query,Guid? categoryId,IBlogRepository repo,BlogService service,CancellationToken ct)=>{
            var lang=locale??"pt";Locale(lang);var data=await repo.ListAsync(lang,Math.Clamp(page??1,1,100000),Math.Clamp(pageSize??6,1,24),query,categoryId,null,true,ct);
            var rows=new List<object>();foreach(var post in data.Items)rows.Add(await service.DtoAsync(post,false,true,ct));return Results.Ok(new{items=rows,data.Total,data.Page,data.PageSize});
        });
        blog.MapGet("/posts/{locale}/{slug}",async(string locale,string slug,IBlogRepository repo,BlogService service,CancellationToken ct)=>{
            Locale(locale);var post=await repo.GetBySlugAsync(locale,slug,ct);return post is null?Results.NotFound():Results.Ok(await service.DtoAsync(post,true,true,ct));
        });
    }
}
