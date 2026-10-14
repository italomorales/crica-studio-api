using CricaStudio.Api.Blog;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace CricaStudio.Application.Tests;
public sealed class BlogSocialAuthenticationTests
{
    private static IConfiguration Config()=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
        ["Cors:AllowedOrigins:0"]="https://www.cricastudio.com.br",["Cors:AllowedOrigins:1"]="https://www.cricastudio.com",["Cors:AllowedOrigins:2"]="http://localhost:5031"
    }).Build();
    [Theory]
    [InlineData("https://www.cricastudio.com.br/blog/pt/uma-ideia",true)]
    [InlineData("https://www.cricastudio.com/blog?idioma=es",true)]
    [InlineData("http://localhost:5031/blog/pt/teste",true)]
    [InlineData("https://www.cricastudio.com.br.evil.example/blog",false)]
    [InlineData("https://www.cricastudio.com.br@evil.example/blog",false)]
    [InlineData("https://evil.example/blog",false)]
    [InlineData("//www.cricastudio.com.br/blog",false)]
    [InlineData("http://www.cricastudio.com.br/blog",false)]
    [InlineData("https://www.cricastudio.com.br/admin",false)]
    [InlineData("https://www.cricastudio.com.br/blog/../admin",false)]
    [InlineData("https://www.cricastudio.com.br/blog#fragment",false)]
    public void OAuth_returns_only_to_an_allowed_public_News_origin(string url,bool valid)=>Assert.Equal(valid,BlogSocialAuthentication.SafeReturn(url,Config()));
    [Fact]
    public void Visitor_principal_does_not_contain_an_admin_token_or_provider_tokens()
    {
        var id=Guid.NewGuid();var principal=BlogSocialAuthentication.Principal(new(id,"Visitante","google"));
        Assert.Equal(BlogSocialAuthentication.Scheme,principal.Identity!.AuthenticationType);
        Assert.Equal(id.ToString(),principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        Assert.Null(principal.FindFirst("sub"));Assert.Null(principal.FindFirst("access_token"));
    }
}
