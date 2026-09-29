using System.Globalization;
using System.Text;

namespace CricaStudio.Api.Catalog;

internal static class ProductSlug
{
    public static string From(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        var dash = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if ((character is >= 'a' and <= 'z') || (character is >= 'A' and <= 'Z') || (character is >= '0' and <= '9'))
            {
                result.Append(char.ToLowerInvariant(character));
                dash = false;
            }
            else if (!dash && result.Length > 0)
            {
                result.Append('-');
                dash = true;
            }
        }
        var slug = result.ToString().Trim('-');
        return slug[..Math.Min(slug.Length, 180)];
    }
}
