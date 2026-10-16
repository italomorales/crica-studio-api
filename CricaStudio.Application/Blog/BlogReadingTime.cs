using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CricaStudio.Application.Blog;

public static class BlogReadingTime
{
    public static int Minutes(JsonElement document)
    {
        var text = new StringBuilder();
        Append(document, text);
        return Math.Max(1, (int)Math.Ceiling(Regex.Matches(text.ToString(), @"\S+").Count / 200d));
    }

    private static void Append(JsonElement node, StringBuilder text)
    {
        if (node.ValueKind != JsonValueKind.Object) return;
        var type = node.TryGetProperty("type", out var kind) ? kind.GetString() : null;
        if (type == "text")
        {
            if (node.TryGetProperty("text", out var value)) text.Append(value.GetString());
            return;
        }
        if (node.TryGetProperty("content", out var children) && children.ValueKind == JsonValueKind.Array)
            foreach (var child in children.EnumerateArray()) Append(child, text);
        text.Append(' ');
    }
}
