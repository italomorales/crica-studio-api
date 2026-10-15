using System.Net.Http.Json;

namespace CricaStudio.Api.Blog;

public sealed record BlogShellManifest(string Html, string[] Styles);
public sealed class BlogFrontendUnavailableException(Exception? inner = null) : Exception("A apresentação do News está temporariamente indisponível.", inner);

/// <summary>Uses a packaged local shell, or the public manifest from the matching frontend release.</summary>
public sealed class BlogFrontendShell(IConfiguration config, IHttpClientFactory clients, TimeProvider time)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private BlogShellManifest? cached;
    private DateTimeOffset expires;

    public async Task<BlogShellManifest> GetAsync(CancellationToken ct)
    {
        var directory = config["Blog:FrontendDirectory"];
        if (string.IsNullOrWhiteSpace(config["Blog:FrontendShellUrl"]) && !string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, "index.html")))
        {
            var assets = Path.Combine(directory, "assets");
            return new(await File.ReadAllTextAsync(Path.Combine(directory, "index.html"), ct),
                Directory.Exists(assets) ? Directory.GetFiles(assets, "blog-*.css").Select(f => "/assets/" + Path.GetFileName(f)).ToArray() : []);
        }
        if (cached is not null && expires > time.GetUtcNow()) return cached;
        await gate.WaitAsync(ct);
        try
        {
            if (cached is not null && expires > time.GetUtcNow()) return cached;
            var url = config["Blog:FrontendShellUrl"] ?? "https://www.cricastudio.com.br/blog-shell.json";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
                || !BlogSocialAuthentication.AllowedOrigin(uri.GetLeftPart(UriPartial.Authority), config))
                throw new BlogFrontendUnavailableException();
            try
            {
                var manifest = await clients.CreateClient("BlogFrontendShell").GetFromJsonAsync<BlogShellManifest>(uri, ct);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.Html) || manifest.Html.Length > 100_000
                    || !manifest.Html.Contains("<crica-app", StringComparison.Ordinal)
                    || !manifest.Html.Contains("<script", StringComparison.Ordinal)
                    || manifest.Styles is null || manifest.Styles.Any(s => !System.Text.RegularExpressions.Regex.IsMatch(s, @"^/assets/blog-[a-zA-Z0-9_-]+\.css$")))
                    throw new BlogFrontendUnavailableException();
                cached = manifest; expires = time.GetUtcNow().AddSeconds(30); return manifest;
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
            {
                if (ct.IsCancellationRequested) throw;
                throw new BlogFrontendUnavailableException(ex);
            }
        }
        finally { gate.Release(); }
    }
}
