using System.Transactions;
using CricaStudio.Domain.Catalog;
using CricaStudio.Infrastructure.Persistence;
using Xunit;
namespace CricaStudio.Application.Tests;
public sealed class PlatformPersistenceTests
{
    [PlatformDatabaseFact]
    public async Task Unified_platforms_preserve_supplier_links_and_filter_localized_storefronts()
    {
        var connectionString = Environment.GetEnvironmentVariable("CRICA_PLATFORMS_TEST_DATABASE")!;
        var platforms = new PostgresPlatformRepository(connectionString);
        var read = new PostgresCatalogReadRepository(connectionString);
        var write = new PostgresCatalogWriteRepository(connectionString);
        var ct = CancellationToken.None;
        var id = Guid.NewGuid();
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            var platform = new CatalogPlatform(id, "Test US platform", "test-" + id.ToString("N"), "English storefront", null, null, "en-US", "US", "draft", 20, false, true);
            await platforms.SaveAsync(platform, true, ct);
            Assert.Contains(await platforms.GetAsync("en-US", "US", true, false, ct), p => p.Id == id);
            Assert.DoesNotContain(await platforms.GetAsync("en-US", "US", true, true, ct), p => p.Id == id);
            platform = platform with { Status = "published", Url = "https://example.com/?ref=123&market=US", LogoUrl = "/assets/platforms/amazon.svg" };
            await platforms.SaveAsync(platform, false, ct);
            Assert.Contains(await platforms.GetAsync("en-US", "US", true, true, ct), p => p.Id == id && p.Url == platform.Url && p.LogoUrl == platform.LogoUrl);
            foreach (var logo in new string?[] { "https://cdn.example.com/catalog/platforms/logo.png", null })
            {
                platform = platform with { LogoUrl = logo };
                await platforms.SaveAsync(platform, false, ct);
                Assert.Equal(logo, (await platforms.GetAsync(null, null, false, false, ct)).Single(p => p.Id == id).LogoUrl);
            }
            Assert.DoesNotContain(await platforms.GetAsync("pt-BR", "BR", true, true, ct), p => p.Id == id);
            var type = (await read.GetAllTypesAsync(ct)).First(t => t.Scope is "suppliers" or "both");
            var supplier = await write.SaveAffiliateProductAsync(new AffiliateProduct(Guid.Empty, type.Id, "Test supplier", "Description", platform.Name, null, null, null, true, false, "draft", 0) { PlatformId = id, IsInternational = true }, ct);
            Assert.Equal(id, (await read.GetAllAffiliateProductsAsync(ct)).Single(p => p.Id == supplier.Id).PlatformId);
            platform = platform with { Name = "Renamed platform", CountryCode = "*" };
            await platforms.SaveAsync(platform, false, ct);
            var persisted = (await read.GetAllAffiliateProductsAsync(ct)).Single(p => p.Id == supplier.Id);
            Assert.Equal(id, persisted.PlatformId); Assert.Equal("Renamed platform", persisted.Platform); Assert.True(persisted.IsInternational);
            Assert.Contains(await platforms.GetAsync("en-US", "BR", true, true, ct), p => p.Id == id && p.ProductCount == 1);
            await platforms.SaveAsync(platform with { Active = false }, false, ct);
            Assert.DoesNotContain(await platforms.GetAsync("en-US", "US", true, true, ct), p => p.Id == id);
            Assert.Equal(id, (await read.GetAllAffiliateProductsAsync(ct)).Single(p => p.Id == supplier.Id).PlatformId);
            // RESTRICT aborts the transaction, so check this last and then roll everything back.
            await Assert.ThrowsAsync<PlatformValidationException>(() => platforms.DeleteAsync(id, ct));
        }
        Assert.DoesNotContain(await platforms.GetAsync(null, null, false, false, ct), p => p.Id == id);
    }
}

public sealed class PlatformDatabaseFactAttribute : FactAttribute
{
    public PlatformDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CRICA_PLATFORMS_TEST_DATABASE")))
            Skip = "Set CRICA_PLATFORMS_TEST_DATABASE to a database with migration 012 applied; all test writes are rolled back.";
    }
}
