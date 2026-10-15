using CricaStudio.Api.Blog;
using Microsoft.Extensions.Configuration;
using Xunit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;
namespace CricaStudio.Application.Tests;
public sealed class BlogSocialAuthenticationTests
{
    [Fact]
    public void Visitor_cookies_are_scoped_to_database_and_not_database_password()
    {
        IConfiguration Database(string connection)=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["ConnectionStrings:Database"]=connection
        }).Build();
        var app=BlogSocialAuthentication.CookieName(Database("Host=localhost;Database=app;Password=first"));
        Assert.NotEqual(app,BlogSocialAuthentication.CookieName(Database("Host=localhost;Database=crica_blog_phase2_local;Password=first")));
        Assert.Equal(app,BlogSocialAuthentication.CookieName(Database("Host=LOCALHOST;Database=app;Password=changed")));
        Assert.DoesNotContain("first",app);Assert.DoesNotContain("app",app);
    }
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
    [Theory]
    [InlineData("BlogTest",false,"test",false)]
    [InlineData("BlogTest",true,"test",true)]
    [InlineData("Production",true,"test",false)]
    [InlineData("BlogTest",false,"google",true)]
    public async Task Test_cookies_are_rejected_when_real_login_is_enabled(string environment,bool testLogin,string provider,bool accepted)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName=environment});
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["BlogSocial:EnableTestLogin"]=testLogin.ToString()});
        builder.AddBlogSocial();
        var authentication=new RecordingAuthentication();builder.Services.AddSingleton<IAuthenticationService>(authentication);
        using var services=builder.Services.BuildServiceProvider();
        var options=services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(BlogSocialAuthentication.Scheme);
        var http=new DefaultHttpContext{RequestServices=services};
        var principal=BlogSocialAuthentication.Principal(new(Guid.NewGuid(),"Visitante",provider));
        var context=new CookieValidatePrincipalContext(http,new AuthenticationScheme(BlogSocialAuthentication.Scheme,null,typeof(CookieAuthenticationHandler)),options,new AuthenticationTicket(principal,BlogSocialAuthentication.Scheme));
        await options.Events.OnValidatePrincipal(context);
        Assert.Equal(accepted,context.Principal is not null);Assert.Equal(accepted?0:1,authentication.Signouts);
    }
    private sealed class RecordingAuthentication:IAuthenticationService
    {
        public int Signouts {get;private set;}
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context,string? scheme)=>Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context,string? scheme,AuthenticationProperties? properties)=>Task.CompletedTask;
        public Task ForbidAsync(HttpContext context,string? scheme,AuthenticationProperties? properties)=>Task.CompletedTask;
        public Task SignInAsync(HttpContext context,string? scheme,ClaimsPrincipal principal,AuthenticationProperties? properties)=>Task.CompletedTask;
        public Task SignOutAsync(HttpContext context,string? scheme,AuthenticationProperties? properties){Signouts++;return Task.CompletedTask;}
    }
}
