using System.Net;
using CricaStudio.Api.Blog;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class BlogFrontendShellTests
{
    private sealed class Responses(string body) : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private static IConfiguration Config(string url = "https://www.cricastudio.com.br/blog-shell.json") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Blog:FrontendDirectory"] = "missing-frontend-directory",
            ["Blog:FrontendShellUrl"] = url,
            ["Cors:AllowedOrigins:0"] = "https://www.cricastudio.com.br"
        }).Build();

    [Fact]
    public async Task Missing_local_package_uses_current_frontend_manifest_and_reuses_only_the_shell()
    {
        var responses = new Responses("""{"html":"<crica-app></crica-app><script src=\"/assets/index-release.js\"></script>","styles":["/assets/blog-release.css"]}""");
        var shell = new BlogFrontendShell(Config(), responses, TimeProvider.System);
        var manifest = await shell.GetAsync(default);
        Assert.Contains("index-release.js", manifest.Html);
        Assert.Equal("/assets/blog-release.css", Assert.Single(manifest.Styles));
        Assert.Same(manifest, await shell.GetAsync(default));
        Assert.Equal(1, responses.Calls);
    }

    [Theory]
    [InlineData("<!doctype html><html>Home</html>")]
    [InlineData("{\"html\":\"Home\",\"styles\":[]}")]
    [InlineData("{\"html\":\"<crica-app><script></script>\",\"styles\":[\"https://foreign.example/evil.css\"]}")]
    public async Task Invalid_manifest_returns_controlled_unavailability(string body)
    {
        var shell = new BlogFrontendShell(Config(), new Responses(body), TimeProvider.System);
        await Assert.ThrowsAsync<BlogFrontendUnavailableException>(() => shell.GetAsync(default));
    }

    [Fact]
    public async Task Unapproved_frontend_origin_is_never_requested()
    {
        var responses = new Responses("");
        var shell = new BlogFrontendShell(Config("https://foreign.example/blog-shell.json"), responses, TimeProvider.System);
        await Assert.ThrowsAsync<BlogFrontendUnavailableException>(() => shell.GetAsync(default));
        Assert.Equal(0, responses.Calls);
    }

    [Theory]
    [InlineData("Uma história")]
    [InlineData("Uma história | Crica Studio")]
    [InlineData("Uma história | Crica Studio | Crica Studio")]
    public void Article_title_has_one_brand_suffix(string title) =>
        Assert.Equal("Uma história | Crica Studio", BlogHtmlEndpoints.PageTitle(title));
}
