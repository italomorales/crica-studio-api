using System.Text.Json;
using System.Text.RegularExpressions;
using CricaStudio.Domain.Blog;

namespace CricaStudio.Application.Blog;

public static partial class BlogPolicy
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
    [GeneratedRegex("^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex LanguagePattern();
    public static bool ValidLocale(string locale) => LanguagePattern().IsMatch(locale);
    public static bool IsPublic(BlogVersion version, DateTimeOffset now) => version.Status == "published" ||
        (version.Status == "scheduled" && version.PublishAt <= now);
    public static void Validate(BlogSave item, string locale, DateTimeOffset now)
    {
        var v = item.Version;
        if(v is null || v.Title is null || v.Summary is null || v.Slug is null || v.SeoTitle is null || v.SeoDescription is null || item.Author is null || item.Cover is null)throw new BlogValidationException("Preencha os campos obrigatórios do cadastro.");
        if (!ValidLocale(locale) || item.CategoryId == Guid.Empty) throw new BlogValidationException("Selecione um idioma e uma categoria válidos.");
        if (item.Author.Length > 120 || item.Cover.Length > 2000 || v.Title.Length > 180 || v.Summary.Length > 400 || v.Slug.Length > 200 || v.SeoTitle.Length > 100 || v.SeoDescription.Length > 180)
            throw new BlogValidationException("Um dos campos excede o tamanho permitido.");
        if (v.Status is not ("draft" or "published" or "scheduled" or "archived")) throw new BlogValidationException("Situação inválida.");
        if (v.Slug.Length > 0 && !SlugPattern().IsMatch(v.Slug)) throw new BlogValidationException("Use letras minúsculas, números e hífens no slug.");
        if (v.Document.ValueKind != JsonValueKind.Object || v.Document.GetRawText().Length > 500_000)
            throw new BlogValidationException("Conteúdo inválido ou muito grande.");
        if (v.Status is "published" or "scheduled")
        {
            if (string.IsNullOrWhiteSpace(v.Title) || string.IsNullOrWhiteSpace(v.Summary) || !SlugPattern().IsMatch(v.Slug) ||
                string.IsNullOrWhiteSpace(item.Author) || string.IsNullOrWhiteSpace(item.Cover) || !HasContent(v.Document))
                throw new BlogValidationException("Preencha título, resumo, endereço, autor, capa e conteúdo antes de publicar.");
            if (v.Status == "scheduled" && (v.PublishAt is null || v.PublishAt <= now)) throw new BlogValidationException("Selecione uma data futura para agendar.");
        }
    }
    private static bool HasContent(JsonElement node)
    {
        if(node.ValueKind!=JsonValueKind.Object)return false;
        if (node.TryGetProperty("text", out var text) && text.ValueKind==JsonValueKind.String && !string.IsNullOrWhiteSpace(text.GetString())) return true;
        if (node.TryGetProperty("type", out var type) && type.ValueKind==JsonValueKind.String && type.GetString() is "image" or "blogProduct") return true;
        return node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array && content.EnumerateArray().Any(HasContent);
    }
}
