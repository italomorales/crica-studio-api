using System.Text.Json;
using CricaStudio.Application.Blog;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class BlogReadingTimeTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(200, 1)]
    [InlineData(201, 2)]
    [InlineData(601, 4)]
    public void Estimates_round_up_at_two_hundred_words_per_minute(int words, int expected)
    {
        var document = JsonSerializer.SerializeToElement(new { type = "doc", content = new[] {
            new { type = "paragraph", content = new[] { new { type = "text", text = string.Join(' ', Enumerable.Repeat("palavra", words)) } } }
        } });
        Assert.Equal(expected, BlogReadingTime.Minutes(document));
    }

    [Fact]
    public void Marked_fragments_stay_joined_and_blocks_separate_words()
    {
        var text = string.Join(' ', Enumerable.Repeat("palavra", 199));
        var document = JsonSerializer.SerializeToElement(new { type = "doc", content = new[] {
            new { type = "paragraph", content = new[] { new { type = "text", text } } },
            new { type = "paragraph", content = new[] { new { type = "text", text = "cri" }, new { type = "text", text = "atividade" } } },
        } });
        Assert.Equal(1, BlogReadingTime.Minutes(document));
        var extra = JsonSerializer.SerializeToElement(new { type = "doc", content = new[] {
            new { type = "paragraph", content = new[] { new { type = "text", text } } },
            new { type = "paragraph", content = new[] { new { type = "text", text = "outras palavras" } } },
        } });
        Assert.Equal(2, BlogReadingTime.Minutes(extra));
    }
}
