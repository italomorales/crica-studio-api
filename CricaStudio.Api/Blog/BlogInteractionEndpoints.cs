using System.Security.Claims;
using CricaStudio.Application.Blog;
using CricaStudio.Domain.Blog;
using CricaStudio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace CricaStudio.Api.Blog;

public sealed record BlogCommentRequest(string Text,Guid? ParentId);
public sealed record BlogModerationRequest(string Status);
public sealed class BlogVisitorWriteFilter(IConfiguration config):IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,EndpointFilterDelegate next){
        var request=context.HttpContext.Request;
        if(request.Headers["X-Crica-Interaction"]!="1")return Results.StatusCode(403);
        var origin=request.Headers.Origin.ToString();
        if(origin.Length>0&&!BlogSocialAuthentication.AllowedOrigin(origin,config)
            && origin!=$"{request.Scheme}://{request.Host}")return Results.StatusCode(403);
        return await next(context);
    }
}
public static class BlogInteractionEndpoints
{
    private static Guid Visitor(ClaimsPrincipal user)=>Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static async Task<Guid?> OptionalVisitor(HttpContext http){var result=await http.AuthenticateAsync(BlogSocialAuthentication.Scheme);return result.Succeeded?Visitor(result.Principal!):null;}
    private static async Task<bool> Published(Guid id,string locale,IBlogRepository repo,CancellationToken ct){
        if(!BlogPolicy.ValidLocale(locale))return false;
        var post=await repo.GetAsync(id,true,ct);return post?.Versions.ContainsKey(locale)==true;
    }
    public static void MapBlogInteractions(this WebApplication app)
    {
        var group=app.MapGroup("/api/blog").AddEndpointFilter<BlogEndpointFilter>();
        group.AddEndpointFilter(async(context,next)=>{context.HttpContext.Response.Headers.CacheControl="private, no-store";return await next(context);});
        group.MapGet("/auth/session",async(HttpContext http,IConfiguration config)=>{
            var auth=await http.AuthenticateAsync(BlogSocialAuthentication.Scheme);
            return Results.Ok(new{user=auth.Succeeded?new BlogVisitor(Visitor(auth.Principal!),auth.Principal!.FindFirstValue(ClaimTypes.Name)??"Visitante",auth.Principal.FindFirstValue("blog_provider")??""):null,
                providers=new{google=BlogSocialAuthentication.Configured(config,"google"),facebook=BlogSocialAuthentication.Configured(config,"facebook")}});
        });
        group.MapGet("/auth/start/{provider}",(string provider,string? returnUrl,IConfiguration config)=>{
            if(!BlogSocialAuthentication.SafeReturn(returnUrl,config))return Results.BadRequest();
            if(!BlogSocialAuthentication.Configured(config,provider))return Results.Problem(statusCode:503,title:"Este login ainda está sendo preparado. Tente novamente em breve.");
            return Results.Challenge(new AuthenticationProperties{RedirectUri=returnUrl},[provider=="google"?"BlogGoogle":"BlogFacebook"]);
        });
        var visitorPolicy=new AuthorizationPolicyBuilder(BlogSocialAuthentication.Scheme).RequireAuthenticatedUser().Build();
        var writes=group.MapGroup("").RequireAuthorization(visitorPolicy).AddEndpointFilter<BlogVisitorWriteFilter>();
        writes.MapPost("/auth/logout",async(HttpContext http)=>{await http.SignOutAsync(BlogSocialAuthentication.Scheme);return Results.NoContent();});
        group.MapGet("/posts/{id:guid}/{locale}/engagement",async(Guid id,string locale,HttpContext http,IBlogRepository repo,PostgresBlogActivityRepository activity,CancellationToken ct)=>
            !await Published(id,locale,repo,ct)?Results.NotFound():Results.Ok(await activity.Engagement(id,locale,await OptionalVisitor(http),ct)));
        group.MapGet("/posts/{id:guid}/{locale}/comments",async(Guid id,string locale,int? page,HttpContext http,IBlogRepository repo,PostgresBlogActivityRepository activity,CancellationToken ct)=>
            !await Published(id,locale,repo,ct)?Results.NotFound():Results.Ok(await activity.Comments(id,locale,Math.Clamp(page??1,1,100000),await OptionalVisitor(http),ct)));
        writes.MapPost("/posts/{id:guid}/{locale}/comments",async(Guid id,string locale,BlogCommentRequest request,ClaimsPrincipal user,IBlogRepository repo,PostgresBlogActivityRepository activity,CancellationToken ct)=>
            !await Published(id,locale,repo,ct)?Results.NotFound():Results.Ok(await activity.Add(id,locale,Visitor(user),"comment",request.Text,request.ParentId,ct)));
        writes.MapPut("/posts/{id:guid}/{locale}/like",async(Guid id,string locale,ClaimsPrincipal user,IBlogRepository repo,PostgresBlogActivityRepository activity,CancellationToken ct)=>
            !await Published(id,locale,repo,ct)?Results.NotFound():Results.Ok(await activity.Add(id,locale,Visitor(user),"like",null,null,ct)));
        writes.MapDelete("/posts/{id:guid}/{locale}/like",async(Guid id,string locale,ClaimsPrincipal user,PostgresBlogActivityRepository activity,CancellationToken ct)=>{await activity.Unlike(id,locale,Visitor(user),ct);return Results.NoContent();});
        writes.MapPost("/posts/{id:guid}/{locale}/share",async(Guid id,string locale,ClaimsPrincipal user,IBlogRepository repo,PostgresBlogActivityRepository activity,CancellationToken ct)=>
            !await Published(id,locale,repo,ct)?Results.NotFound():Results.Ok(await activity.Add(id,locale,Visitor(user),"share",null,null,ct)));
        var admin=app.MapGroup("/api/admin/blog/activity").RequireAuthorization().AddEndpointFilter<BlogEndpointFilter>();
        admin.MapGet("",async(string? status,string? kind,int? page,int? pageSize,PostgresBlogActivityRepository activity,CancellationToken ct)=>{
            if(status is not(null or "" or "pending" or "approved" or "rejected" or "spam")||kind is not(null or "" or "comment" or "like" or "share"))return Results.BadRequest();
            return Results.Ok(await activity.Moderation(status??"",kind??"",Math.Clamp(page??1,1,100000),Math.Clamp(pageSize??20,1,100),ct));
        });
        admin.MapPut("/{id:guid}",async(Guid id,BlogModerationRequest request,ClaimsPrincipal user,PostgresBlogActivityRepository activity,CancellationToken ct)=>
            await activity.Moderate(id,request.Status,Guid.TryParse(user.FindFirstValue("sub"),out var adminId)?adminId:null,ct)?Results.NoContent():Results.NotFound());
        // No test identity endpoint is registered in Production/Development.
        if(app.Environment.IsEnvironment("BlogTest")&&app.Configuration.GetValue<bool>("BlogSocial:EnableTestLogin")){
            group.MapPost("/auth/test",async(string? subject,HttpContext http,PostgresBlogActivityRepository activity,CancellationToken ct)=>{
                if(http.Connection.RemoteIpAddress is not {} ip||!System.Net.IPAddress.IsLoopback(ip))return Results.NotFound();
                var visitor=await activity.Visitor("test",subject??"local-interaction-test","Visitante de teste local",ct);
                await http.SignInAsync(BlogSocialAuthentication.Scheme,BlogSocialAuthentication.Principal(visitor));return Results.Ok(visitor);
            }).AddEndpointFilter<BlogVisitorWriteFilter>();
            group.MapGet("/auth/test-page",(HttpContext http,string? returnUrl,IConfiguration config)=>{
                if(http.Connection.RemoteIpAddress is not {} ip||!System.Net.IPAddress.IsLoopback(ip)||!BlogSocialAuthentication.SafeReturn(returnUrl,config))return Results.NotFound();
                var target=System.Text.Json.JsonSerializer.Serialize(returnUrl);
                return Results.Content("<html lang=\"pt\"><meta charset=\"utf-8\"><title>Teste local de interação</title><h1>Visitante de teste local</h1><p>Somente no banco isolado. Nenhuma conta Google ou Facebook é acessada.</p><button onclick=\"entrar()\">Entrar como visitante de teste local</button><script>async function entrar(){const r=await fetch('/api/blog/auth/test',{method:'POST',headers:{'X-Crica-Interaction':'1'}});if(r.ok)location.assign("+target+");}</script></html>","text/html");
            });
        }
    }
}
