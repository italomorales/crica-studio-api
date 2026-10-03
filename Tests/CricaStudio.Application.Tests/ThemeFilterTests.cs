using CricaStudio.Api.Catalog;
using CricaStudio.Domain.Catalog;
using CricaStudio.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class ThemeFilterTests
{
    [Theory]
    [InlineData(null, true, 0)]
    [InlineData("", true, 0)]
    [InlineData("00000000-0000-0000-0000-000000000000", false, 0)]
    [InlineData("natal", false, 0)]
    [InlineData("12345678-1234-4234-8234-123456789abc,12345678-1234-4234-8234-123456789abc", true, 1)]
    public void Parses_only_valid_distinct_theme_ids(string? value, bool valid, int count)
    {
        Assert.Equal(valid, CatalogEndpoints.TryParseThemeIds(value, out var ids));
        Assert.Equal(count, ids.Length);
    }

    [PostgresThemeFact]
    public async Task Themes_preserve_links_filter_with_OR_and_paginate_without_duplicates()
    {
        var connectionString = Environment.GetEnvironmentVariable("CRICA_THEMES_TEST_DATABASE")!;
        // This test provisions schema only in an explicitly named disposable test database.
        Assert.Equal("crica_themes_test", new NpgsqlConnectionStringBuilder(connectionString).Database);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using (var reset = new NpgsqlCommand("DROP SCHEMA IF EXISTS cricastudio CASCADE", connection))
                await reset.ExecuteNonQueryAsync();
            foreach (var file in new[] { "002_create_catalog.sql", "006_add_affiliate_product_featured.sql", "007_add_affiliate_product_images.sql", "008_add_shop_product_slug.sql", "009_add_product_themes.sql", "010_add_affiliate_product_international.sql" })
            {
                await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "sql", file)), connection);
                await command.ExecuteNonQueryAsync();
            }
        }
        var read = new PostgresCatalogReadRepository(connectionString);
        var write = new PostgresCatalogWriteRepository(connectionString);
        var ct = CancellationToken.None;
        var supplierType = await write.SaveTypeAsync(new CatalogType(Guid.Empty, "Materiais", "suppliers", true), ct);
        var affiliate = await write.SaveAffiliateProductAsync(new AffiliateProduct(Guid.Empty, supplierType.Id, "Material importado", "Descrição", "Shopee", null, null, null, true, true, "published", 0) { IsInternational = true }, ct);
        Assert.True((await read.GetAllAffiliateProductsAsync(ct)).Single(p => p.Id == affiliate.Id).IsInternational);
        Assert.True((await read.GetPublishedAffiliateProductPageAsync(1, 12, null, null, true, ct)).Items.Single(p => p.Id == affiliate.Id).IsInternational);
        await write.SaveAffiliateProductAsync(affiliate with { IsInternational = false }, ct);
        Assert.False((await read.GetPublishedAffiliateProductsAsync(ct)).Single(p => p.Id == affiliate.Id).IsInternational);
        var mugs = await write.SaveTypeAsync(new CatalogType(Guid.Empty, "Canecas", "shop", true), ct);
        var buttons = await write.SaveTypeAsync(new CatalogType(Guid.Empty, "Bottons", "shop", true), ct);
        var christmas = await write.SaveThemeAsync(new CatalogTheme(Guid.Empty, "Natal", true), ct);
        var memes = await write.SaveThemeAsync(new CatalogTheme(Guid.Empty, "Memes", true), ct);
        var unused = await write.SaveThemeAsync(new CatalogTheme(Guid.Empty, "Sem produtos", true), ct);
        await Assert.ThrowsAsync<CatalogThemeValidationException>(() => write.SaveThemeAsync(new CatalogTheme(Guid.Empty, "natal", true), ct));
        ShopProduct Product(string name, Guid typeId, params Guid[] themes) => new(Guid.Empty, typeId, name, name.ToLower().Replace(' ', '-'), "Descrição", null, "consult", null, false, false, [], [], [new ProductImage(Guid.NewGuid(), "/assets/hero.webp", 0)], "published", 0) { ThemeIds = themes };
        var both = await write.SaveShopProductAsync(Product("Caneca divertida", mugs.Id, christmas.Id, memes.Id, memes.Id), ct);
        var memeButton = await write.SaveShopProductAsync(Product("Botton meme", buttons.Id, memes.Id), ct);
        var plain = await write.SaveShopProductAsync(Product("Caneca lisa", mugs.Id), ct);
        var draft = await write.SaveShopProductAsync(Product("Rascunho", mugs.Id, christmas.Id) with { Status = "draft" }, ct);
        var themes = await read.GetThemesAsync(true, ct);
        Assert.Equal(2, themes.Count);
        Assert.Equal(1, themes.Single(t => t.Id == christmas.Id).ProductCount);
        Assert.Equal(2, themes.Single(t => t.Id == memes.Id).ProductCount);
        Assert.Equal(2, (await read.GetAllShopProductsAsync(ct)).Single(p => p.Id == both.Id).ThemeIds.Count);
        var first = await read.GetPublishedShopProductPageAsync(1, 1, null, null, false, ct, [christmas.Id, memes.Id]);
        var second = await read.GetPublishedShopProductPageAsync(2, 1, null, null, false, ct, [christmas.Id, memes.Id]);
        Assert.Equal(2, first.Total);
        Assert.NotEqual(first.Items.Single().Id, second.Items.Single().Id);
        var mugsOnly = await read.GetPublishedShopProductPageAsync(1, 12, "divertida", mugs.Id, false, ct, [christmas.Id, memes.Id]);
        Assert.Equal(both.Id, mugsOnly.Items.Single().Id);
        Assert.Equal(3, (await read.GetPublishedShopProductPageAsync(1, 12, null, null, false, ct)).Total);
        await Assert.ThrowsAsync<CatalogThemeValidationException>(() => write.DeleteThemeAsync(christmas.Id, ct));
        await write.SaveThemeAsync(christmas with { IsActive = false }, ct);
        Assert.DoesNotContain(await read.GetThemesAsync(true, ct), t => t.Id == christmas.Id);
        Assert.Equal(0, (await read.GetPublishedShopProductPageAsync(1, 12, null, null, false, ct, [christmas.Id])).Total);
        Assert.Contains(christmas.Id, (await read.GetAllShopProductsAsync(ct)).Single(p => p.Id == both.Id).ThemeIds);
        await write.SaveShopProductAsync(both with { Name = "Caneca editada" }, ct);
        await Assert.ThrowsAsync<CatalogThemeValidationException>(() => write.SaveShopProductAsync(plain with { ThemeIds = [christmas.Id] }, ct));
        await Assert.ThrowsAsync<CatalogThemeValidationException>(() => write.SaveShopProductAsync(plain with { ThemeIds = [Guid.NewGuid()] }, ct));
        Assert.Empty((await read.GetAllShopProductsAsync(ct)).Single(p => p.Id == plain.Id).ThemeIds);
        await write.SaveShopProductAsync(both with { ThemeIds = [] }, ct);
        Assert.Empty((await read.GetAllShopProductsAsync(ct)).Single(p => p.Id == both.Id).ThemeIds);
        await write.DeleteShopProductAsync(draft.Id, ct);
        await write.DeleteThemeAsync(christmas.Id, ct);
        await write.DeleteThemeAsync(unused.Id, ct);
        // Retain active examples for the local browser/API validation.
        await write.SaveShopProductAsync(both with { ThemeIds = [memes.Id] }, ct);
    }
}

public sealed class PostgresThemeFactAttribute : FactAttribute
{
    public PostgresThemeFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CRICA_THEMES_TEST_DATABASE")))
            Skip = "Set CRICA_THEMES_TEST_DATABASE to a disposable crica_themes_test database.";
    }
}
