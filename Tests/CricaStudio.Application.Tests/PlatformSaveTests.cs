using System.Text.Json;
using CricaStudio.Api.Catalog;
using CricaStudio.Domain.Catalog;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class PlatformSaveTests
{
    [Fact]
    public async Task Published_Amazon_with_bundled_logo_and_no_storefront_link_is_saved()
    {
        const string payload = """
            {"name":"Amazon","code":"amazon","description":"Veja a seleção da Crica disponível na Amazon","url":null,"logoUrl":"/assets/platforms/amazon.svg","locale":"en-US","countryCode":"US","status":"published","order":60,"mobileOnly":false,"active":true,"id":"11400000-0000-4000-8000-000000000006"}
            """;
        var item = JsonSerializer.Deserialize<CatalogPlatform>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var repository = new CapturingRepository();
        var result = await PlatformEndpoints.Save(item, false, repository, CancellationToken.None);
        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(item, repository.Saved);
        Assert.False(repository.Created);
    }

    private sealed class CapturingRepository : IPlatformRepository
    {
        public CatalogPlatform? Saved { get; private set; }
        public bool Created { get; private set; }
        public Task<CatalogPlatform?> SaveAsync(CatalogPlatform platform, bool create, CancellationToken ct)
        {
            Saved = platform; Created = create;
            return Task.FromResult<CatalogPlatform?>(platform);
        }
        public Task<IReadOnlyList<CatalogPlatform>> GetAsync(string? locale, string? country, bool publicOnly, bool storefrontsOnly, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
