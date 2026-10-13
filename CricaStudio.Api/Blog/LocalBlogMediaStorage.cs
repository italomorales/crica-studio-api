using CricaStudio.Api.Catalog;

namespace CricaStudio.Api.Blog;

/// <summary>Real file uploads for explicit local development/test environments; production remains S3.</summary>
public sealed class LocalBlogMediaStorage(CatalogMediaOptions options) : ICatalogMediaStorage
{
    public async Task<string> UploadAsync(string category,IFormFile file,CancellationToken ct)
    {
        if(category!="blog")throw new CatalogMediaValidationException("O armazenamento local do blog aceita somente imagens do blog.");
        var extension=file.ContentType switch{"image/png"=>"png","image/jpeg"=>"jpg","image/webp"=>"webp",_=>null};
        if(extension is null||file.Length is <=0 or >5*1024*1024)throw new CatalogMediaValidationException("Envie PNG, JPEG ou WebP de até 5 MB.");
        await using var stream=file.OpenReadStream();
        if(!await S3CatalogMediaStorage.HasExpectedSignatureAsync(stream,file.ContentType,ct))throw new CatalogMediaValidationException("Assinatura de imagem inválida.");
        stream.Position=0;Directory.CreateDirectory(options.LocalDirectory);
        var name=$"{Guid.NewGuid():N}.{extension}";
        await using var output=File.Create(Path.Combine(options.LocalDirectory,name));await stream.CopyToAsync(output,ct);
        return options.PublicBaseUrl.TrimEnd('/')+"/catalog/blog/"+name;
    }
}
