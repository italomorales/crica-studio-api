using CricaStudio.Api.Catalog;
using Xunit;
namespace CricaStudio.Application.Tests;
public sealed class PlatformValidationTests
{
    [Theory]
    [InlineData("/assets/construction/shopee.svg", true)]
    [InlineData("/assets/construction/mercado-livre.svg", true)]
    [InlineData("/assets/construction/tiktok-shop.png", true)]
    [InlineData("/assets/platforms/aliexpress.svg", true)]
    [InlineData("/assets/platforms/amazon.svg", true)]
    [InlineData("/assets/platforms/temu.svg", true)]
    [InlineData("/assets/platforms/../private.svg", false)]
    [InlineData("/assets/platforms/temu.svg?other=1", false)]
    [InlineData("//evil.example/logo.svg", false)]
    [InlineData("data:image/svg+xml;base64,PHN2Zz4=", false)]
    [InlineData("https://cdn.example.com/catalog/platforms/logo.png", true)]
    public void Accepts_only_bundled_or_https_logos(string url, bool expected) => Assert.Equal(expected, PlatformEndpoints.SafeLogoUrl(url));
    [Theory]
    [InlineData("pt-BR", "br", "pt-BR", "BR")]
    [InlineData("en-US", "US", "en-US", "US")]
    [InlineData("es-ES", "*", "es-ES", "*")]
    public void Normalizes_market(string locale, string country, string expectedLocale, string expectedCountry)
    {
        Assert.True(PlatformEndpoints.TryMarket(locale, country, out var actualLocale, out var actualCountry));
        Assert.Equal(expectedLocale, actualLocale);
        Assert.Equal(expectedCountry, actualCountry);
    }
    [Theory]
    [InlineData("pt", "BR")]
    [InlineData("", "BR")]
    [InlineData("pt-BR", "ZZ")]
    [InlineData("en-US", "USA")]
    public void Rejects_invalid_market(string locale, string country) => Assert.False(PlatformEndpoints.TryMarket(locale, country, out _, out _));
    [Theory]
    [InlineData(null, true)]
    [InlineData("https://meli.la/19kz47o", true)]
    [InlineData("https://example.com/?affiliate=123&market=BR", true)]
    [InlineData("https://user:secret@example.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("http://example.com", false)]
    public void Requires_safe_https_links(string? url, bool expected) => Assert.Equal(expected, PlatformEndpoints.SafeUrl(url));
}

