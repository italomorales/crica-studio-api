namespace CricaStudio.Domain.Blog;

public sealed record BlogVisitor(Guid Id,string Name,string Provider);
public sealed record BlogActivity(Guid Id,Guid PostId,string Locale,Guid VisitorId,string Name,string Kind,string Text,Guid? ParentId,string Status,DateTimeOffset Date,string PostTitle,string? ParentText);
public sealed record BlogEngagement(int Likes,int Comments,string? MyLikeStatus);
