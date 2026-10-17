using System.Text.Json;

namespace CricaStudio.Domain.Blog;

public sealed record BlogCategory(Guid Id, string Name, string Slug, bool Active, Dictionary<string,string> Translations);
public sealed record BlogVersion(string Title, string Summary, string Slug, JsonElement Document, string SeoTitle,
    string SeoDescription, string Status, DateTimeOffset? PublishAt, DateTimeOffset? PublishedAt, DateTimeOffset UpdatedAt);
public sealed record BlogPost(Guid Id, Guid CategoryId, string Category, string Cover, string Author, DateOnly Date,
    bool Featured, Dictionary<string,BlogVersion> Versions, DateTimeOffset UpdatedAt)
{
    public Guid? AuthorId { get; init; }
    public BlogAuthor? AuthorProfile { get; init; }
    public Dictionary<string,string> CategoryTranslations { get; init; } = [];
}
public sealed record BlogAuthor(Guid Id,string Name,string Photo,string Bio,bool Active);
public sealed record BlogPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record BlogProduct(Guid Id, string Kind, string Name, string Description, string? Image, string Href, string Type);
public sealed record BlogSave(Guid CategoryId, string Cover, string Author, DateOnly Date, bool Featured, BlogVersion Version)
{
    public Guid? AuthorId { get; init; }
}
public sealed class BlogValidationException(string message) : Exception(message);

public interface IBlogRepository
{
    Task<BlogPage<BlogPost>> ListAsync(string locale, int page, int pageSize, string? query, Guid? categoryId, string? status, bool publicOnly, CancellationToken ct);
    Task<BlogPost?> GetAsync(Guid id, bool publicOnly, CancellationToken ct);
    Task<BlogPost?> GetBySlugAsync(string locale, string slug, CancellationToken ct);
    Task<BlogPost> SaveAsync(Guid? id, string locale, BlogSave item, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<BlogCategory>> CategoriesAsync(bool publicOnly, CancellationToken ct);
    Task<BlogCategory> SaveCategoryAsync(BlogCategory item, CancellationToken ct);
    Task DeleteCategoryAsync(Guid id, CancellationToken ct);
    Task<BlogPage<BlogProduct>> ProductsAsync(string locale, string kind, string? query, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<BlogProduct>> ResolveProductsAsync(string locale, IReadOnlyList<Guid> shopIds, IReadOnlyList<Guid> platformIds, CancellationToken ct);
    Task RecordImageAsync(Guid id, string url, Guid? userId, CancellationToken ct);
    Task<bool> ImageExistsAsync(string url, CancellationToken ct);
    Task<IReadOnlyList<BlogPost>> SitemapAsync(CancellationToken ct);
    Task<IReadOnlyList<BlogAuthor>> AuthorsAsync(CancellationToken ct);
    Task<BlogAuthor?> GetAuthorAsync(Guid id,CancellationToken ct);
    Task<BlogAuthor> SaveAuthorAsync(BlogAuthor item,CancellationToken ct);
    Task DeleteAuthorAsync(Guid id,CancellationToken ct);
}
