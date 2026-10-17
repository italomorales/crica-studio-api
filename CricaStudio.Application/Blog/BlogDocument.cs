using System.Net;
using System.Text.Json;
using CricaStudio.Domain.Blog;

namespace CricaStudio.Application.Blog;

/// <summary>Render the structured Tiptap document, never arbitrary author-supplied HTML.</summary>
public static class BlogDocument
{
    private static readonly HashSet<string> Nodes = ["doc","paragraph","heading","text","bulletList","orderedList","listItem","blockquote","hardBreak","horizontalRule","image","blogProduct","codeBlock"];
    private static readonly HashSet<string> Marks = ["bold","italic","underline","strike","code","link"];
    public static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
    public static bool SafeLink(string? url) => url is not null && ((url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\')) ||
        (Uri.TryCreate(url,UriKind.Absolute,out var uri) && (uri.Scheme=="https" || uri.Scheme=="http"&&uri.IsLoopback) && string.IsNullOrEmpty(uri.UserInfo)));
    public static void Validate(JsonElement document)
    {
        var count=0;
        void Visit(JsonElement node,int depth)
        {
            if(depth>40 || ++count>5000 || node.ValueKind!=JsonValueKind.Object || !node.TryGetProperty("type",out var type) || type.ValueKind!=JsonValueKind.String || !Nodes.Contains(type.GetString()??"")) throw new BlogValidationException("O conteúdo contém um bloco inválido ou excede o limite.");
            if(node.TryGetProperty("text",out var text) && text.ValueKind!=JsonValueKind.String)throw new BlogValidationException("Texto inválido no documento.");
            if(node.TryGetProperty("marks",out var markList)&&markList.ValueKind!=JsonValueKind.Array)throw new BlogValidationException("Formatação inválida no documento.");
            if(node.TryGetProperty("marks",out var marks)) foreach(var mark in marks.EnumerateArray())
            {
                if(mark.ValueKind!=JsonValueKind.Object || !mark.TryGetProperty("type",out var m) || m.ValueKind!=JsonValueKind.String || !Marks.Contains(m.GetString()??"")) throw new BlogValidationException("Formatação não permitida.");
                if(m.GetString()=="link" && !SafeLink(Attr(mark,"href"))) throw new BlogValidationException("Link inválido no conteúdo.");
            }
            if(type.GetString()=="image" && !SafeLink(Attr(node,"src"))) throw new BlogValidationException("Use imagens enviadas para o blog, sem data URLs.");
            if(type.GetString()=="blogProduct" && (!Guid.TryParse(Attr(node,"id"),out var id) || id==Guid.Empty || Attr(node,"kind") is not ("shop" or "platform"))) throw new BlogValidationException("Selecione um produto existente da loja ou vitrine.");
            if(node.TryGetProperty("content",out var content))
            {
                if(content.ValueKind!=JsonValueKind.Array)throw new BlogValidationException("Estrutura de conteúdo inválida.");
                foreach(var child in content.EnumerateArray())Visit(child,depth+1);
            }
        }
        if(document.ValueKind!=JsonValueKind.Object || !document.TryGetProperty("type",out var root) || root.GetString()!="doc")throw new BlogValidationException("O conteúdo deve ser um documento Tiptap.");
        Visit(document,0);
    }
    public static string? Attr(JsonElement node,string name) => node.TryGetProperty("attrs",out var attrs)&&attrs.ValueKind==JsonValueKind.Object&&attrs.TryGetProperty(name,out var value) ? value.ValueKind==JsonValueKind.String?value.GetString():value.ToString():null;
    public static IEnumerable<JsonElement> Walk(JsonElement node)
    {
        yield return node;
        if(node.TryGetProperty("content",out var children)&&children.ValueKind==JsonValueKind.Array)foreach(var child in children.EnumerateArray())foreach(var nested in Walk(child))yield return nested;
    }
    public static string Render(JsonElement document,IReadOnlyList<BlogProduct> products,string locale="pt")
    {
        string Node(JsonElement node)
        {
            var type=node.GetProperty("type").GetString();
            var inner=node.TryGetProperty("content",out var children)?string.Concat(children.EnumerateArray().Select(Node)):"";
            if(type=="text")
            {
                var text=Encode(node.TryGetProperty("text",out var value)?value.GetString():"");
                if(node.TryGetProperty("marks",out var marks))foreach(var mark in marks.EnumerateArray())
                {
                    var kind=mark.GetProperty("type").GetString();var tag=kind switch{"bold"=>"strong","italic"=>"em","underline"=>"u","strike"=>"s","code"=>"code",_=>null};
                    if(tag is not null)text=$"<{tag}>{text}</{tag}>";
                    else if(kind=="link"&&SafeLink(Attr(mark,"href")))text=$"<a href=\"{Encode(Attr(mark,"href"))}\" rel=\"noopener noreferrer\">{text}</a>";
                }
                return text;
            }
            if(type=="image")return $"<img src=\"{Encode(Attr(node,"src"))}\" alt=\"{Encode(Attr(node,"alt"))}\" loading=\"lazy\">";
            if(type=="blogProduct")
            {
                var product=products.FirstOrDefault(p=>p.Id.ToString()==Attr(node,"id")&&p.Kind==Attr(node,"kind"));
                if(product is null || !SafeLink(product.Href))return "";
                return $"<aside class=\"blog-product-card\" data-blog-product=\"{product.Id}\" data-kind=\"{product.Kind}\"><img src=\"{Encode(SafeLink(product.Image)?product.Image:"/assets/crica-studio-logo-transparent.png")}\" alt=\"{Encode(product.Name)}\"><div><small>{Encode(BlogPublicCopy.Text(product.Type,locale))}</small><h3>{Encode(product.Name)}</h3><p>{Encode(product.Description)}</p><a href=\"{Encode(product.Href)}\"{(product.Kind=="platform"?" target=\"_blank\" rel=\"sponsored noopener noreferrer\"":"")}>{BlogPublicCopy.Text("Ver produto",locale)} →</a></div></aside>";
            }
            var element=type switch{"paragraph"=>"p","heading"=>Attr(node,"level")=="3"?"h3":"h2","bulletList"=>"ul","orderedList"=>"ol","listItem"=>"li","blockquote"=>"blockquote","codeBlock"=>"pre",_=>null};
            return type switch {"hardBreak"=>"<br>","horizontalRule"=>"<hr>",_=>element is null?inner:$"<{element}>{inner}</{element}>"};
        }
        return Node(document);
    }
}
