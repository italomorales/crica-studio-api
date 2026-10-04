using System.Transactions;
using CricaStudio.Api.Catalog;
using CricaStudio.Domain.Catalog;
using CricaStudio.Infrastructure.Persistence;
using Xunit;

namespace CricaStudio.Application.Tests;
public sealed class PlatformMarketTests
{
    [Theory]
    [InlineData(null, true, false)]
    [InlineData("br", true, false)]
    [InlineData("international", true, true)]
    [InlineData("all", false, false)]
    public void Validates_market_scope(string? market, bool valid, bool international)
    {
        Assert.Equal(valid, CatalogEndpoints.TryMarketScope(market, out var actual));
        Assert.Equal(international, actual);
    }

    [PlatformDatabaseFact]
    public async Task Locale_separates_products_totals_pagination_and_featured_independently_of_imported_flag()
    {
        var connection = Environment.GetEnvironmentVariable("CRICA_PLATFORMS_TEST_DATABASE")!;
        var platforms = new PostgresPlatformRepository(connection);
        var read = new PostgresCatalogReadRepository(connection);
        var write = new PostgresCatalogWriteRepository(connection);
        var ct = CancellationToken.None;
        var key = "marketaudit" + Guid.NewGuid().ToString("N")[..20];
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var br = new CatalogPlatform(Guid.NewGuid(), key + " BR", key + "-br", "Brazil", null, null, "pt-BR", "BR", "published", 0, false, true);
        var us = br with { Id = Guid.NewGuid(), Name = key + " US", Code = key + "-us", Locale = "en-US", CountryCode = "US" };
        await platforms.SaveAsync(br, true, ct); await platforms.SaveAsync(us, true, ct);
        var type = (await read.GetAllTypesAsync(ct)).First(t => t.Scope is "suppliers" or "both");
        foreach (var platform in new[] { br, us })
            for (var i = 0; i < 2; i++)
                await write.SaveAffiliateProductAsync(new AffiliateProduct(Guid.Empty, type.Id, key + i, "Test", platform.Name, null, null, null, true, i == 0, "published", i)
                    { PlatformId = platform.Id, IsInternational = platform.Id == br.Id }, ct);
        foreach (var international in new[] { false, true })
        {
            var expected = international ? us.Id : br.Id;
            var page1 = await read.GetPublishedAffiliateProductPageAsync(1, 1, key, null, false, ct, [type.Id], international);
            var page2 = await read.GetPublishedAffiliateProductPageAsync(2, 1, key, null, false, ct, [type.Id], international);
            Assert.Equal(2, page1.Total); Assert.Equal(2, page2.Total);
            Assert.Equal(expected, page1.Items.Single().PlatformId); Assert.Equal(expected, page2.Items.Single().PlatformId);
            Assert.NotEqual(page1.Items.Single().Id, page2.Items.Single().Id);
            var featured = await read.GetPublishedAffiliateProductPageAsync(1, 12, key, null, true, ct, international: international);
            Assert.Equal(1, featured.Total); Assert.Equal(expected, featured.Items.Single().PlatformId);
            var vitrines = await PlatformEndpoints.ReadPublic(platforms, "pt-BR", "BR", true, true, ct, international ? "international" : "br");
            Assert.Contains(vitrines, p => p.Id == expected);
            Assert.DoesNotContain(vitrines, p => p.Id == (international ? br.Id : us.Id));
        }
        await platforms.SaveAsync(us with { Active = false }, false, ct);
        Assert.Equal(0, (await read.GetPublishedAffiliateProductPageAsync(1, 12, key, null, false, ct, international: true)).Total);
    }
}
