using System.Text.Json.Nodes;
namespace CricaStudio.Domain.Catalog;

public sealed record CatalogTranslation(string Status, JsonObject Fields);
public interface ITranslationInput { Dictionary<string, CatalogTranslation>? Translations { get; } }
public sealed record CatalogLanguage(string Code, string Name, string NativeName, string? FlagCode, bool Active, int Order);
public sealed class TranslationValidationException(string message) : Exception(message);
public interface ITranslationRepository
{
    Task<IReadOnlyList<CatalogLanguage>> LanguagesAsync(bool publicOnly, CancellationToken ct);
    Task<CatalogLanguage> SaveLanguageAsync(CatalogLanguage item, CancellationToken ct);
    Task<bool> DeleteLanguageAsync(string code, CancellationToken ct);
    Task<Dictionary<Guid,Dictionary<string,CatalogTranslation>>> ReadAsync(string resource, IReadOnlyList<Guid> ids, bool publicOnly, CancellationToken ct);
}
public static class TranslationRules
{
    public static readonly Dictionary<string,string> Tables = new()
    { ["types"]="type_translations", ["themes"]="theme_translations", ["affiliates"]="affiliate_translations", ["platforms"]="platform_translations" };
    public static void Validate(string resource, Dictionary<string,CatalogTranslation>? translations)
    {
        if (translations is null) return;
        if (!Tables.ContainsKey(resource)) throw new TranslationValidationException("Este cadastro não possui traduções.");
        if (translations.Count > 100) throw new TranslationValidationException("Use até 100 idiomas por cadastro.");
        foreach (var (code, translation) in translations)
        {
            if (code == "pt" || !System.Text.RegularExpressions.Regex.IsMatch(code,"^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")) throw new TranslationValidationException("Informe um código de idioma válido; português é o conteúdo original.");
            if (translation is null || translation.Fields is null || translation.Status is not ("pending" or "draft" or "reviewed")) throw new TranslationValidationException("Informe um status de tradução válido.");
            string[] allowed = resource switch {
                "affiliates" or "platforms" => ["name","description"],
                "types" or "themes" => ["name"], _ => [] };
            foreach (var (field,value) in translation.Fields)
            {
                if (!allowed.Contains(field)) throw new TranslationValidationException("A tradução contém um campo não permitido.");
                if (value is null) continue;
                if (field is "characteristics" or "personalization") {
                    if (value is not JsonArray array || array.Count > 100 || array.Any(v=>v is not JsonValue || !v.AsValue().TryGetValue<string>(out var s) || s.Length > 500)) throw new TranslationValidationException("Informe listas de até 100 textos com até 500 caracteres.");
                } else {
                    var limit = field == "name" ? resource is "types" or "themes" or "platforms" ? 120 : 180 : field == "description" ? 500 : 20000;
                    if (value is not JsonValue scalar || !scalar.TryGetValue<string>(out var s) || s.Length > limit) throw new TranslationValidationException($"Confira o campo {field}; limite de {limit} caracteres.");
                }
            }
        }
    }
    public static JsonNode? Field(CatalogTranslation? translation, string field) {
        if (translation?.Status != "reviewed" || !translation.Fields.TryGetPropertyValue(field,out var value)) return null;
        return value switch { JsonValue scalar when scalar.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) => value,
            JsonArray array when array.Any(v=>v is JsonValue scalar && scalar.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)) => new JsonArray(array.Where(v=>v is JsonValue scalar && scalar.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)).Select(v=>v!.DeepClone()).ToArray()), _ => null };
    }
}
