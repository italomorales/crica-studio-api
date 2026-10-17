using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CricaStudio.Application.Blog;
using CricaStudio.Domain.Blog;

namespace CricaStudio.Api.Blog;

/// <summary>Public HTML origin for the CDN's /blog behaviors; assets remain on the frontend origin.</summary>
public static class BlogHtmlEndpoints
{
    private static string E(string? s)=>BlogDocument.Encode(s);
    private static string Copy(string locale,string text)=>E(BlogPublicCopy.Text(text,locale));
    private static string Category(BlogPost post,string locale)=>Copy(locale,post.CategoryTranslations.GetValueOrDefault(locale,post.Category));
    private static string Path(string locale,BlogVersion v)=>$"/blog/{locale}/{v.Slug}";
    private static string ListingLocale(HttpContext http,IConfiguration config)
    {
        var explicitLocale=http.Request.Query["idioma"].FirstOrDefault();
        if(explicitLocale is not null)return explicitLocale;
        var host=http.Request.Host.Host.ToLowerInvariant();
        if(host is not ("cricastudio.com" or "www.cricastudio.com" or "cricastudio.com.br" or "www.cricastudio.com.br"))
            host=new Uri(Site(config,http)).Host.ToLowerInvariant();
        if(host is not ("cricastudio.com" or "www.cricastudio.com"))return "pt";
        var preferred=http.Request.GetTypedHeaders().AcceptLanguage?.Where(item=>item.Quality!=0)
            .OrderByDescending(item=>item.Quality??1).FirstOrDefault()?.Value.ToString()??"";
        return preferred.Equals("es",StringComparison.OrdinalIgnoreCase)||preferred.StartsWith("es-",StringComparison.OrdinalIgnoreCase)?"es":"en";
    }
    private static string Site(IConfiguration config,HttpContext http)
    {
        var origin=http.Request.Scheme+"://"+http.Request.Host;
        if(BlogSocialAuthentication.AllowedOrigin(origin,config))return origin;
        // Public metadata may select another known site domain. Authentication still requires the private proxy key.
        var viewerHost=http.Request.Headers["x-crica-viewer-host"].ToString().ToLowerInvariant();
        if(viewerHost is "www.cricastudio.com.br" or "cricastudio.com.br" or "www.cricastudio.com" or "cricastudio.com")
        {
            var viewerOrigin="https://"+viewerHost;
            if(BlogSocialAuthentication.AllowedOrigin(viewerOrigin,config))return viewerOrigin;
        }
        return (config["Blog:PublicSiteUrl"]??"https://www.cricastudio.com.br").TrimEnd('/');
    }
    public static string PageTitle(string title)=>Regex.Replace(title.Trim(),@"(?:\s*[|–—-]\s*Crica Studio)+\s*$","",RegexOptions.IgnoreCase).Trim()+" | Crica Studio";
    private static async Task<string> Shell(IConfiguration config,BlogFrontendShell frontend,string locale,string title,string description,string canonical,string body,string extra,bool indexable,CancellationToken ct)
    {
        var manifest=await frontend.GetAsync(ct);
        var source=manifest.Html;
        var assets=string.Join("\n",Regex.Matches(source,"<script\\b[^>]*>[\\s\\S]*?</script>|<link\\b[^>]*(?:stylesheet|modulepreload)[^>]*>",RegexOptions.IgnoreCase).Select(m=>m.Value));
        var css=manifest.Styles.Select(path=>$"<link rel=\"stylesheet\" href=\"{E(path)}\">");
        return $"<!doctype html><html lang=\"{E(locale)}\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><base href=\"/\"><title>{E(PageTitle(title))}</title><meta name=\"description\" content=\"{E(description)}\"><meta name=\"robots\" content=\"{(indexable?"index,follow":"noindex,nofollow")}\"><link rel=\"canonical\" href=\"{E(canonical)}\"><meta property=\"og:title\" content=\"{E(title)}\"><meta property=\"og:description\" content=\"{E(description)}\"><meta property=\"og:url\" content=\"{E(canonical)}\"><meta property=\"og:site_name\" content=\"Crica Studio\"><meta name=\"twitter:card\" content=\"summary_large_image\">{extra}{assets}{string.Join("",css)}</head><body><crica-app><header class=\"container\"><a href=\"/\"><img width=\"140\" src=\"/assets/crica-studio-logo-transparent.png\" alt=\"Crica Studio\"></a><nav><a href=\"/\">Home</a> · <a href=\"/blog\">News</a> · <a href=\"/loja\">Loja</a> · <a href=\"/fornecedores\">Fornecedores</a> · <a href=\"/vitrine\">Vitrines</a></nav></header>{body}</crica-app></body></html>";
    }
    private static object AuthorSchema(BlogPost post,string site)
    {
        var author=new Dictionary<string,object>{{"@type","Person"},{"name",post.Author}};
        if(post.AuthorProfile is { } profile){if(profile.Photo.Length>0)author["image"]=profile.Photo.StartsWith('/')?site+profile.Photo:profile.Photo;}
        return author;
    }
    private static string AuthorMarkup(BlogPost post)
    {
        var photo=post.AuthorProfile?.Photo;
        var avatar=!string.IsNullOrEmpty(photo)&&BlogDocument.SafeLink(photo)?$"<img class=\"blog-avatar blog-author-photo\" src=\"{E(photo)}\" alt=\"{E(post.Author)}\" width=\"60\" height=\"60\">":$"<span class=\"blog-avatar\" aria-hidden=\"true\">{E(post.Author.Length>0?post.Author[..1]:"C")}</span>";
        return $"<div class=\"blog-byline\">{avatar}<div><strong>{E(post.Author)}</strong><div class=\"blog-meta\"><time datetime=\"{post.Date:yyyy-MM-dd}\">{post.Date:dd/MM/yyyy}</time></div></div></div>";
    }
    private static string AuthorAboutMarkup(BlogPost post,string locale)
    {
        if(post.AuthorProfile is not {} author)return "";
        var label=locale=="en"?"About the author":locale=="es"?"Sobre el autor":"Sobre o autor";
        var photo=!string.IsNullOrWhiteSpace(author.Photo)&&BlogDocument.SafeLink(author.Photo)
            ?$"<img class=\"blog-author-card-photo\" src=\"{E(author.Photo)}\" alt=\"{E(author.Name)}\" width=\"72\" height=\"72\" loading=\"lazy\">"
            :$"<span class=\"blog-author-card-photo blog-author-card-initial\" aria-hidden=\"true\">{E(author.Name.Length>0?author.Name[..1]:"")}</span>";
        return $"<section class=\"blog-author-card\" aria-labelledby=\"blog-about-author-name\">{photo}<div class=\"blog-author-card-copy\"><span class=\"blog-author-card-label\">{label}</span><h2 id=\"blog-about-author-name\">{E(author.Name)}</h2>{(string.IsNullOrEmpty(author.Bio)?"":$"<p>{Copy(locale,author.Bio)}</p>")}</div></section>";
    }
    public static void MapBlogHtmlEndpoints(this WebApplication app)
    {
        var group=app.MapGroup("/blog").AddEndpointFilter<BlogEndpointFilter>();
        group.MapGet("",async(HttpContext http,IConfiguration config,BlogFrontendShell frontend,IBlogRepository repo,CancellationToken ct)=>{
            var locale=ListingLocale(http,config);
            if(!BlogPolicy.ValidLocale(locale))return Results.NotFound();
            var page=int.TryParse(http.Request.Query["pagina"],out var p)?Math.Clamp(p,1,100000):1;
            var query=http.Request.Query["q"].ToString();
            var categoryId=Guid.TryParse(http.Request.Query["categoria"],out var cat)?(Guid?)cat:null;
            var data=await repo.ListAsync(locale,page,6,query,categoryId,null,true,ct);
            var categories=await repo.CategoriesAsync(true,ct);
            var body=new StringBuilder($"<main id=\"main\" class=\"blog-page container\"><header class=\"blog-intro\"><span class=\"eyebrow\">CRICA NEWS</span><h1>{Copy(locale,"Entre uma ideia")}<br><span>{Copy(locale,"e outra.")}</span></h1><p>{Copy(locale,"Inspiração, histórias e detalhes para criar algo que tenha a sua essência.")}</p></header>");
            body.Append($"<form class=\"blog-search\" action=\"/blog\"><input type=\"hidden\" name=\"idioma\" value=\"{E(locale)}\"><label>{Copy(locale,"Buscar matérias")}<input name=\"q\" value=\"{E(query)}\" type=\"search\"></label><button>{Copy(locale,"Buscar")}</button></form><nav class=\"blog-categories\"><a href=\"/blog?idioma={E(locale)}\">{Copy(locale,"Todas")}</a>");
            foreach(var c in categories)body.Append($"<a href=\"/blog?idioma={E(locale)}&amp;categoria={c.Id}\">{Copy(locale,c.Translations.GetValueOrDefault(locale,c.Name))}</a>");
            body.Append("</nav><section class=\"blog-feed blog-grid\">");
            foreach(var post in data.Items){var v=post.Versions[locale];body.Append($"<a class=\"blog-card\" href=\"{E(Path(locale,v))}\"><div class=\"blog-cover\"><img src=\"{E(post.Cover)}\" alt=\"{E(v.Title)}\" loading=\"lazy\"></div><div class=\"blog-card-copy\"><span class=\"eyebrow\">{Category(post,locale)}</span><h2>{E(v.Title)}</h2><p>{E(v.Summary)}</p><time datetime=\"{post.Date:yyyy-MM-dd}\">{post.Date:dd/MM/yyyy}</time></div></a>");}
            if(data.Total==0)body.Append($"<p>{Copy(locale,"Nenhuma matéria encontrada.")}</p>");
            body.Append("</section><nav class=\"blog-pagination\">");
            string Page(int n)=>$"/blog?idioma={locale}&amp;pagina={n}&amp;q={Uri.EscapeDataString(query)}"+(categoryId.HasValue?$"&amp;categoria={categoryId}":"");
            if(page>1)body.Append($"<a href=\"{Page(page-1)}\">← {Copy(locale,"Anterior")}</a>");
            if(page*6<data.Total)body.Append($"<a href=\"{Page(page+1)}\">{Copy(locale,"Próxima")} →</a>");
            body.Append("</nav></main>");
            http.Response.Headers.CacheControl="no-cache";
            http.Response.Headers.Vary="Accept-Language, Host";
            return Results.Content(await Shell(config,frontend,locale,"Crica News",BlogPublicCopy.Text("Inspiração e histórias da Crica Studio.",locale),Site(config,http)+"/blog"+(locale=="pt"?"":$"?idioma={locale}"),body.ToString(),"",query.Length==0&&categoryId is null&&page==1,ct),"text/html; charset=utf-8");
        });
        group.MapGet("/{locale}/{slug}",async(string locale,string slug,HttpContext http,IConfiguration config,BlogFrontendShell frontend,IBlogRepository repo,BlogService service,CancellationToken ct)=>{
            if(!BlogPolicy.ValidLocale(locale))return Results.NotFound();
            var post=await repo.GetBySlugAsync(locale,slug,ct);
            if(post is null||!post.Versions.TryGetValue(locale,out var v))return Results.NotFound();
            var content=await service.ContentAsync(v,locale,false,ct);
            var canonical=Site(config,http)+Path(locale,v);
            var extra=new StringBuilder($"<meta property=\"og:type\" content=\"article\"><meta property=\"og:image\" content=\"{E(post.Cover.StartsWith('/')?Site(config,http)+post.Cover:post.Cover)}\">");
            foreach(var (lang,version) in post.Versions)extra.Append($"<link data-blog-alternate=\"true\" rel=\"alternate\" hreflang=\"{E(lang)}\" href=\"{E(Site(config,http)+Path(lang,version))}\">");
            extra.Append("<script id=\"blog-article-schema\" type=\"application/ld+json\">"+JsonSerializer.Serialize(new Dictionary<string,object?>{{"@context","https://schema.org"},{"@type","Article"},{"headline",v.Title},{"description",v.Summary},{"image",post.Cover.StartsWith('/')?Site(config,http)+post.Cover:post.Cover},{"datePublished",v.PublishedAt??v.PublishAt},{"dateModified",v.UpdatedAt},{"inLanguage",locale},{"author",AuthorSchema(post,Site(config,http))},{"mainEntityOfPage",canonical}})+"</script>");
            var body=$"<main id=\"main\" class=\"blog-page container\"><a class=\"back-link\" href=\"/blog?idioma={E(locale)}\">← {Copy(locale,"Todas as histórias")}</a><article class=\"blog-article\"><header class=\"blog-article-heading\"><span class=\"eyebrow\">{Category(post,locale)}</span><h1>{E(v.Title)}</h1><p class=\"blog-deck\">{E(v.Summary)}</p>{AuthorMarkup(post)}</header><figure class=\"blog-article-cover\"><img src=\"{E(post.Cover)}\" alt=\"{E(v.Title)}\"></figure><div class=\"blog-prose\">{content.Html}</div></article>{AuthorAboutMarkup(post,locale)}</main>";
            http.Response.Headers.CacheControl="no-cache";
            return Results.Content(await Shell(config,frontend,locale,string.IsNullOrWhiteSpace(v.SeoTitle)?v.Title:v.SeoTitle,string.IsNullOrWhiteSpace(v.SeoDescription)?v.Summary:v.SeoDescription,canonical,body,extra.ToString(),true,ct),"text/html; charset=utf-8");
        });
        app.MapGet("/blog-sitemap.xml",async(HttpContext http,IConfiguration config,IBlogRepository repo,CancellationToken ct)=>{
            XNamespace ns="http://www.sitemaps.org/schemas/sitemap/0.9";XNamespace xhtml="http://www.w3.org/1999/xhtml";
            var posts=await repo.SitemapAsync(ct);
            var xml=new XElement(ns+"urlset",new XAttribute(XNamespace.Xmlns+"xhtml",xhtml));
            foreach(var post in posts) foreach(var (lang,version) in post.Versions)
            {
                var url=new XElement(ns+"url",new XElement(ns+"loc",Site(config,http)+Path(lang,version)),new XElement(ns+"lastmod",version.UpdatedAt.ToString("O")));
                foreach(var (alternate,v) in post.Versions)url.Add(new XElement(xhtml+"link",new XAttribute("rel","alternate"),new XAttribute("hreflang",alternate),new XAttribute("href",Site(config,http)+Path(alternate,v))));
                xml.Add(url);
            }
            return Results.Content(new XDocument(new XDeclaration("1.0","utf-8",null),xml).ToString(),"application/xml; charset=utf-8");
        }).AddEndpointFilter<BlogEndpointFilter>();
    }
}
