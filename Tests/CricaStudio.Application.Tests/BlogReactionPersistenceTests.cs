using CricaStudio.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class LocalBlogReactionDatabaseFactAttribute:FactAttribute
{
    public LocalBlogReactionDatabaseFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BLOG_REACTIONS_TEST_DATABASE")))Skip="Requer o banco local isolado de interações.";}
}
public sealed class BlogReactionPersistenceTests
{
    [LocalBlogReactionDatabaseFact]
    public async Task Likes_are_immediate_and_unique_while_comments_and_replies_remain_moderated()
    {
        var connectionString=Environment.GetEnvironmentVariable("BLOG_REACTIONS_TEST_DATABASE")!;
        var settings=new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal("crica_blog_phase2_local",settings.Database);
        Assert.Contains(settings.Host,new[]{"127.0.0.1","localhost"});
        var posts=new PostgresBlogRepository(connectionString);
        var activity=new PostgresBlogActivityRepository(connectionString);
        var ct=CancellationToken.None;
        var post=await posts.GetBySlugAsync("pt","autor-b7c99e8b",ct);Assert.NotNull(post);
        var visitor=await activity.Visitor("test","reaction-"+Guid.NewGuid(),"Teste local de curtida",ct);
        var baseline=await activity.Engagement(post.Id,"pt",null,ct);
        var like=await activity.Add(post.Id,"pt",visitor.Id,"like",null,null,ct);
        Assert.Equal("approved",like.Status);
        Assert.Equal(baseline.Likes+1,(await activity.Engagement(post.Id,"pt",visitor.Id,ct)).Likes);
        Assert.Equal(like.Id,(await activity.Add(post.Id,"pt",visitor.Id,"like",null,null,ct)).Id);
        Assert.Equal(baseline.Likes+1,(await activity.Engagement(post.Id,"pt",null,ct)).Likes);
        Assert.False(await activity.Moderate(like.Id,"spam",null,ct));
        Assert.Equal(0,(await activity.Moderation("","like",1,100,ct)).Total);
        await activity.Unlike(post.Id,"pt",visitor.Id,ct);
        Assert.Equal(baseline.Likes,(await activity.Engagement(post.Id,"pt",null,ct)).Likes);
        Assert.Null((await activity.Engagement(post.Id,"pt",visitor.Id,ct)).MyLikeStatus);
        var share=await activity.Add(post.Id,"pt",visitor.Id,"share",null,null,ct);
        Assert.Equal("approved",share.Status);
        Assert.False(await activity.Moderate(share.Id,"rejected",null,ct));
        Assert.Equal(0,(await activity.Moderation("","share",1,100,ct)).Total);
        var comment=await activity.Add(post.Id,"pt",visitor.Id,"comment","Comentário de validação local",null,ct);
        Assert.Equal("pending",comment.Status);
        Assert.Equal(baseline.Comments,(await activity.Engagement(post.Id,"pt",null,ct)).Comments);
        Assert.All((await activity.Moderation("","",1,100,ct)).Items,row=>Assert.Equal("comment",row.Kind));
        Assert.True(await activity.Moderate(comment.Id,"approved",null,ct));
        var replier=await activity.Visitor("test","reaction-reply-"+Guid.NewGuid(),"Resposta local",ct);
        var reply=await activity.Add(post.Id,"pt",replier.Id,"comment","Resposta de validação local",comment.Id,ct);
        Assert.Equal("pending",reply.Status);
        Assert.Equal(baseline.Comments+1,(await activity.Engagement(post.Id,"pt",null,ct)).Comments);
        Assert.True(await activity.Moderate(reply.Id,"approved",null,ct));
        Assert.Equal(baseline.Comments+2,(await activity.Engagement(post.Id,"pt",null,ct)).Comments);
        Assert.True(await activity.Moderate(comment.Id,"rejected",null,ct));
        Assert.Equal(baseline.Comments,(await activity.Engagement(post.Id,"pt",null,ct)).Comments);
    }
}
