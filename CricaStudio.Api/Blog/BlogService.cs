using System.Text.Json;
using CricaStudio.Application.Blog;
using CricaStudio.Domain.Blog;

namespace CricaStudio.Api.Blog;

public sealed class BlogService(IBlogRepository repository,TimeProvider time)
{
    private static readonly JsonElement EmptyDocument = JsonSerializer.SerializeToElement(new { type = "doc", content = Array.Empty<object>() });
    public async Task<(string Html,IReadOnlyList<BlogProduct> Products)> ContentAsync(BlogVersion version,string locale,bool validateReferences,CancellationToken ct)
    {
        BlogDocument.Validate(version.Document);
        var nodes=BlogDocument.Walk(version.Document).ToArray();
        var cards=nodes.Where(n=>n.GetProperty("type").GetString()=="blogProduct").ToArray();
        var shop=cards.Where(n=>BlogDocument.Attr(n,"kind")=="shop").Select(n=>Guid.Parse(BlogDocument.Attr(n,"id")!)).Distinct().ToArray();
        var platforms=cards.Where(n=>BlogDocument.Attr(n,"kind")=="platform").Select(n=>Guid.Parse(BlogDocument.Attr(n,"id")!)).Distinct().ToArray();
        var products=await repository.ResolveProductsAsync(locale,shop,platforms,ct);
        if(validateReferences&&products.Count!=shop.Length+platforms.Length)throw new BlogValidationException("Um produto associado foi removido ou não está publicado.");
        if(validateReferences)foreach(var image in nodes.Where(n=>n.GetProperty("type").GetString()=="image"))
            if(!await ValidImageAsync(BlogDocument.Attr(image,"src")!,ct))throw new BlogValidationException("Envie as imagens pela ferramenta de upload do blog.");
        return (BlogDocument.Render(version.Document,products),products);
    }
    public async Task<bool> ValidImageAsync(string url,CancellationToken ct) =>
        url.StartsWith("/assets/",StringComparison.Ordinal)&&!url.Contains("..")&&!url.Contains('\\')&&BlogDocument.SafeLink(url) ||
        (BlogDocument.SafeLink(url)&&await repository.ImageExistsAsync(url,ct));
    public async Task<BlogSave> ResolveAuthorAsync(BlogSave item,CancellationToken ct)
    {
        if(item.AuthorId is not Guid authorId)return item;
        var author=await repository.GetAuthorAsync(authorId,ct) ?? throw new BlogValidationException("Autor não encontrado.");
        return item with{Author=author.Name};
    }
    public async Task<BlogAuthor> SaveAuthorAsync(BlogAuthor author,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(author.Name)||author.Name.Length>120||author.Bio is null||author.Bio.Length>600||author.Photo is null||author.Photo.Length>2000)throw new BlogValidationException("Preencha um nome de até 120 caracteres e uma apresentação de até 600 caracteres.");
        if(author.Photo.Length>0&&!await ValidImageAsync(author.Photo,ct))throw new BlogValidationException("Envie a foto pela ferramenta de upload.");
        return await repository.SaveAuthorAsync(author,ct);
    }
    public async Task<BlogPost> SaveAsync(Guid? id,string locale,BlogSave item,CancellationToken ct)
    {
        item=await ResolveAuthorAsync(item,ct);
        BlogPolicy.Validate(item,locale,time.GetUtcNow());
        if(item.Cover.Length>0&&!await ValidImageAsync(item.Cover,ct))throw new BlogValidationException("Envie a capa pela ferramenta de upload do blog.");
        await ContentAsync(item.Version,locale,item.Version.Status is "published" or "scheduled",ct);
        return await repository.SaveAsync(id,locale,item,ct);
    }
    public async Task<object> DtoAsync(BlogPost post,bool detail,bool publicOnly,CancellationToken ct)
    {
        var versions=new Dictionary<string,object>();var allProducts=new List<BlogProduct>();
        foreach(var (locale,v) in post.Versions)
        {
            var content=detail?await ContentAsync(v,locale,false,ct):("",(IReadOnlyList<BlogProduct>)Array.Empty<BlogProduct>());
            allProducts.AddRange(content.Item2);
            versions[locale]=new {v.Title,v.Summary,v.Slug,document=detail?v.Document:EmptyDocument,html=content.Item1,readingMinutes=BlogReadingTime.Minutes(v.Document),v.SeoTitle,v.SeoDescription,
                status=publicOnly&&BlogPolicy.IsPublic(v,time.GetUtcNow())?"published":v.Status,v.PublishAt,v.PublishedAt,v.UpdatedAt};
        }
        return new {post.Id,post.CategoryId,post.Category,post.Cover,post.Author,post.AuthorId,post.AuthorProfile,post.Date,post.Featured,post.UpdatedAt,versions,products=allProducts.DistinctBy(p=>(p.Kind,p.Id))};
    }
}
