using System.Security.Claims;
using CricaStudio.Domain.Blog;
using CricaStudio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Npgsql;

namespace CricaStudio.Api.Blog;

public static class BlogSocialAuthentication
{
    public const string Scheme="BlogVisitor";
    public static string CookieName(IConfiguration config)
    {
        var database=new NpgsqlConnectionStringBuilder(config.GetConnectionString("Database")??"");
        var scope=$"{database.Host?.ToLowerInvariant()}:{database.Port}/{database.Database}";
        var suffix=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope)))[..16].ToLowerInvariant();
        return "crica.blog.visitor."+suffix;
    }
    public static bool Configured(IConfiguration config,string provider)=>
        provider is "google" or "facebook" && !string.IsNullOrWhiteSpace(config[$"BlogSocial:{provider}:ClientId"]) && !string.IsNullOrWhiteSpace(config[$"BlogSocial:{provider}:ClientSecret"]);
    public static bool AllowedOrigin(string origin,IConfiguration config)=>
        (config.GetSection("Cors:AllowedOrigins").Get<string[]>()??[]).Contains(origin,StringComparer.OrdinalIgnoreCase);
    public static bool SafeReturn(string? url,IConfiguration config)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)
        && uri.UserInfo.Length==0 && uri.Fragment.Length==0 && (uri.Scheme=="https" || uri.IsLoopback&&uri.Scheme=="http")
        && AllowedOrigin(uri.GetLeftPart(UriPartial.Authority),config) && (uri.AbsolutePath=="/blog"||uri.AbsolutePath.StartsWith("/blog/",StringComparison.Ordinal));
    public static ClaimsPrincipal Principal(BlogVisitor visitor)=>new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier,visitor.Id.ToString()),new Claim(ClaimTypes.Name,visitor.Name),new Claim("blog_provider",visitor.Provider)
    ],Scheme));
    public static void AddBlogSocial(this WebApplicationBuilder builder)
    {
        var auth=builder.Services.AddAuthentication();
        auth.AddCookie(Scheme,options=>{
            // Local API ports share browser cookies; keep visitor IDs scoped to their database.
            options.Cookie.Name=CookieName(builder.Configuration);options.Cookie.HttpOnly=true;options.Cookie.SameSite=SameSiteMode.Lax;
            options.Cookie.SecurePolicy=builder.Environment.IsDevelopment()||builder.Environment.IsEnvironment("BlogTest")?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
            options.ExpireTimeSpan=TimeSpan.FromDays(14);options.SlidingExpiration=true;
            options.Events.OnRedirectToLogin=context=>{context.Response.StatusCode=401;return Task.CompletedTask;};
            options.Events.OnRedirectToAccessDenied=context=>{context.Response.StatusCode=403;return Task.CompletedTask;};
            options.Events.OnValidatePrincipal=async context=>{
                if(context.Principal?.FindFirst("blog_provider")?.Value=="test"
                    && !(builder.Environment.IsEnvironment("BlogTest")&&builder.Configuration.GetValue<bool>("BlogSocial:EnableTestLogin"))){
                    context.RejectPrincipal();await context.HttpContext.SignOutAsync(Scheme);
                }
            };
        });
        void Configure(OAuthOptions options,string provider){
            options.SignInScheme=Scheme;options.ClientId=builder.Configuration[$"BlogSocial:{provider}:ClientId"]!;options.ClientSecret=builder.Configuration[$"BlogSocial:{provider}:ClientSecret"]!;
            options.CallbackPath=$"/api/blog/auth/callback/{provider}";options.SaveTokens=false;
            options.Events.OnCreatingTicket=async context=>{
                var subject=context.Identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if(string.IsNullOrWhiteSpace(subject))throw new InvalidOperationException("O provedor não retornou uma identidade.");
                var repo=context.HttpContext.RequestServices.GetRequiredService<PostgresBlogActivityRepository>();
                var visitor=await repo.Visitor(provider,subject,context.Identity?.FindFirst(ClaimTypes.Name)?.Value??"Visitante",context.HttpContext.RequestAborted);
                context.Principal=Principal(visitor);context.Properties.IsPersistent=true;
            };
            options.Events.OnRemoteFailure=context=>{
                context.HandleResponse();var destination=context.Properties?.RedirectUri;
                if(!SafeReturn(destination,builder.Configuration))destination=(builder.Configuration["Blog:PublicSiteUrl"]??"https://www.cricastudio.com.br")+"/blog";
                context.Response.Redirect(destination+(destination!.Contains('?')?"&":"?")+"blogLogin=failed");return Task.CompletedTask;
            };
        }
        if(Configured(builder.Configuration,"google"))auth.AddGoogle("BlogGoogle",options=>{Configure(options,"google");options.Scope.Clear();options.Scope.Add("profile");options.UsePkce=true;});
        if(Configured(builder.Configuration,"facebook"))auth.AddFacebook("BlogFacebook",options=>{Configure(options,"facebook");options.Scope.Clear();options.Scope.Add("public_profile");});
    }
}
