using CricaStudio.Api.Catalog;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class CatalogApiValidationTests
{
    [Theory]
    [InlineData("https://cricastudio.com/catalog/products/image.webp")]
    [InlineData("https://cdn.example.com/image.png")]
    public void Accepts_https_image_urls(string url) =>
        Assert.True(AdminCatalogEndpoints.IsAllowedImageUrl(url));

    [Theory]
    [InlineData("http://cdn.example.com/image.png")]
    [InlineData("https://user:password@cdn.example.com/image.png")]
    [InlineData("javascript:alert(1)")]
    public void Rejects_unsafe_image_urls(string url) =>
        Assert.False(AdminCatalogEndpoints.IsAllowedImageUrl(url));

    [Fact]
    public void Accepts_local_asset_urls() =>
        Assert.True(AdminCatalogEndpoints.IsAllowedImageUrl("/assets/images/product.webp"));

    [Fact]
    public void Media_options_require_bucket_and_public_url()
    {
        var options = new CatalogMediaOptions();

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
