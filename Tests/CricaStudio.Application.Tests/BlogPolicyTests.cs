using System.Text.Json;
using CricaStudio.Application.Blog;
using CricaStudio.Domain.Blog;
using Xunit;
namespace CricaStudio.Application.Tests;
public sealed class BlogPolicyTests
{
    private static JsonElement Doc(string json)=>JsonDocument.Parse(json).RootElement.Clone();
    [Fact]
    public void Scheduled_versions_become_public_only_at_their_own_time()
    {
        var now=DateTimeOffset.UtcNow;
        var v=new BlogVersion("title","summary","slug",Doc("{\"type\":\"doc\",\"content\":[]}"),"","","scheduled",now.AddHours(1),null,now);
        Assert.False(BlogPolicy.IsPublic(v,now));Assert.True(BlogPolicy.IsPublic(v,now.AddHours(1)));
        Assert.False(BlogPolicy.IsPublic(v with{Status="draft"},now.AddDays(1)));
        Assert.False(BlogPolicy.IsPublic(v with{Status="archived"},now.AddDays(1)));
    }
    [Theory]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"script\"}]}")]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"text\":42}]}")]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"marks\":\"bold\"}]}")]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"image\",\"attrs\":{\"src\":\"javascript:alert(1)\"}}]}")]
    public void Invalid_document_shapes_are_validation_errors(string json)=>Assert.Throws<BlogValidationException>(()=>BlogDocument.Validate(Doc(json)));
    [Fact]
    public void Text_and_attributes_are_encoded_and_removed_products_do_not_leave_dead_links()
    {
        var doc=Doc("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"<script>x</script>\"}]},{\"type\":\"blogProduct\",\"attrs\":{\"id\":\"00000000-0000-4000-8000-000000000001\",\"kind\":\"shop\"}}]}");
        BlogDocument.Validate(doc);var html=BlogDocument.Render(doc,[]);
        Assert.DoesNotContain("<script>",html);Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("<aside",html);
    }
}
