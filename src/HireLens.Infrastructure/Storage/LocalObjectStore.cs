using Microsoft.AspNetCore.Hosting;

namespace HireLens.Infrastructure.Storage;

/// <summary>
/// Development stand-in for SAP Object Store. Production binds S3 via VCAP
/// and returns a real presigned URL so bytes never transit the API process.
/// Until then, bytes land on a writable local directory (CF containers run as non-root).
/// </summary>
public sealed class LocalObjectStore(IWebHostEnvironment environment) : IObjectStore
{
    private readonly string _root = ObjectStorePaths.ResolveRoot(environment.ContentRootPath);

    public Task<string> CreateUploadUrlAsync(string objectKey, string contentType, CancellationToken cancellationToken)
    {
        _ = contentType;
        _ = cancellationToken;
        var segments = objectKey.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var encoded = string.Join('/', segments.Select(Uri.EscapeDataString));
        return Task.FromResult("/api/object-store/" + encoded);
    }

    public async Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
    {
        _ = contentType;
        var path = ObjectStorePaths.PhysicalPath(_root, objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream> GetAsync(string objectKey, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var path = ObjectStorePaths.PhysicalPath(_root, objectKey);
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }
}

internal static class ObjectStorePaths
{
    internal static string ResolveRoot(string contentRoot)
    {
        var configured = Environment.GetEnvironmentVariable("OBJECT_STORE_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            Directory.CreateDirectory(configured);
            return configured;
        }

        var appData = Path.Combine(contentRoot, "App_Data", "object-store");
        if (IsWritableDirectory(appData))
        {
            return appData;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "hirelens-object-store");
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    internal static string PhysicalPath(string root, string objectKey)
    {
        var relative = objectKey.Replace('\\', '/').TrimStart('/');
        var combined = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var normalizedRoot = Path.GetFullPath(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Object key escapes the store root.");
        }

        return combined;
    }

    private static bool IsWritableDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
