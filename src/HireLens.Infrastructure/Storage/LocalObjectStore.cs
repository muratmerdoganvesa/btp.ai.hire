using Microsoft.AspNetCore.Hosting;

namespace HireLens.Infrastructure.Storage;

/// <summary>
/// Development stand-in for SAP Object Store. Production binds S3 via VCAP
/// and returns a real presigned URL so bytes never transit the API process.
/// </summary>
public sealed class LocalObjectStore(IWebHostEnvironment environment) : IObjectStore
{
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "object-store");

    public Task<string> CreateUploadUrlAsync(string objectKey, string contentType, CancellationToken cancellationToken)
    {
        _ = contentType;
        _ = cancellationToken;
        return Task.FromResult("/api/object-store/" + objectKey.Replace('\\', '/'));
    }

    public async Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
    {
        _ = contentType;
        var path = Path.Combine(Root, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream> GetAsync(string objectKey, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var path = Path.Combine(Root, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }
}
