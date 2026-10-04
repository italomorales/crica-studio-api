using CricaStudio.Api.Catalog;
using CricaStudio.Domain.Catalog;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class PublicStorefrontTests
{
    [Fact]
    public async Task Global_directory_includes_published_international_stores_without_drafts_or_inactive_stores()
    {
        var repo = new RecordingRepository();
        var rows = await PlatformEndpoints.ReadPublic(repo, "pt-BR", "BR", true, true, CancellationToken.None);
        Assert.Equal(new[] { "Amazon" }, rows.Select(p => p.Name));
        Assert.Equal((null, null, false, true), repo.Query);
    }
    [Fact]
    public async Task Supplier_filters_and_regional_vitrines_keep_their_market_scope()
    {
        var repo = new RecordingRepository();
        await PlatformEndpoints.ReadPublic(repo, "pt-BR", "BR", false, true, CancellationToken.None);
        Assert.Equal(("pt-BR", "BR", true, false), repo.Query);
        await PlatformEndpoints.ReadPublic(repo, "en-US", "US", true, false, CancellationToken.None);
        Assert.Equal(("en-US", "US", true, true), repo.Query);
    }
    private sealed class RecordingRepository : IPlatformRepository
    {
        public (string? Locale, string? Country, bool PublicOnly, bool Storefronts) Query;
        public Task<IReadOnlyList<CatalogPlatform>> GetAsync(string? locale, string? country, bool publicOnly, bool storefrontsOnly, CancellationToken ct)
        {
            Query = (locale, country, publicOnly, storefrontsOnly);
            var amazon = new CatalogPlatform(Guid.NewGuid(), "Amazon", "amazon", "Vitrine", null, "/assets/platforms/amazon.svg", "en-US", "US", "published", 60, false, true);
            return Task.FromResult<IReadOnlyList<CatalogPlatform>>([amazon, amazon with { Name = "Draft", Status = "draft" }, amazon with { Name = "Inactive", Active = false }]);
        }
        public Task<CatalogPlatform?> SaveAsync(CatalogPlatform platform, bool create, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
