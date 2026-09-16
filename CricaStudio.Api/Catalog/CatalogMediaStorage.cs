using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace CricaStudio.Api.Catalog;

public sealed class CatalogMediaOptions
{
    public const string SectionName = "CatalogMedia";

    public string Bucket { get; init; } = string.Empty;
    public string Region { get; init; } = "sa-east-1";
    public string PublicBaseUrl { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Bucket) || string.IsNullOrWhiteSpace(PublicBaseUrl))
            throw new InvalidOperationException("Defina CatalogMedia__Bucket e CatalogMedia__PublicBaseUrl no ambiente da API.");
    }
}

public interface ICatalogMediaStorage
{
    Task<string> UploadAsync(string category, IFormFile file, CancellationToken cancellationToken);
}

public sealed class S3CatalogMediaStorage(CatalogMediaOptions options) : ICatalogMediaStorage
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
    };

    public async Task<string> UploadAsync(string category, IFormFile file, CancellationToken cancellationToken)
    {
        options.Validate();
        if (category is not ("products" or "affiliates"))
            throw new CatalogMediaValidationException("Destino de mídia inválido.");
        if (file.Length is <= 0 or > MaxFileSize)
            throw new CatalogMediaValidationException("Escolha uma imagem de até 5 MB.");
        if (!Extensions.TryGetValue(file.ContentType, out var extension))
            throw new CatalogMediaValidationException("Envie uma imagem PNG, JPG ou WebP.");

        await using var input = file.OpenReadStream();
        if (!await HasExpectedSignatureAsync(input, file.ContentType, cancellationToken))
            throw new CatalogMediaValidationException("O arquivo não corresponde ao formato de imagem informado.");
        input.Position = 0;

        var key = $"catalog/{category}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}.{extension}";
        using var s3 = new AmazonS3Client(RegionEndpoint.GetBySystemName(options.Region));
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = key,
            InputStream = input,
            ContentType = file.ContentType,
        }, cancellationToken);

        return $"{options.PublicBaseUrl.TrimEnd('/')}/{key}";
    }

    private static async Task<bool> HasExpectedSignatureAsync(Stream stream, string contentType, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var length = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        return contentType switch
        {
            "image/jpeg" => length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => length >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            _ => false,
        };
    }
}

public sealed class CatalogMediaValidationException(string message) : Exception(message);
