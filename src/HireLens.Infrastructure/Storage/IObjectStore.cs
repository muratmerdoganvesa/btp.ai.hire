using HireLens.SharedKernel;

namespace HireLens.Infrastructure.Storage;

public interface IObjectStore
{
    Task<string> CreateUploadUrlAsync(string objectKey, string contentType, CancellationToken cancellationToken);

    Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken);

    Task<Stream> GetAsync(string objectKey, CancellationToken cancellationToken);
}

public interface IFileGuard
{
    Result Scan(string fileName, string contentType, ReadOnlySpan<byte> header);
}

public sealed class FileGuard : IFileGuard
{
    public Result Scan(string fileName, string contentType, ReadOnlySpan<byte> header)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".pdf" || contentType == "application/pdf")
        {
            if (header.Length >= 5 && header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F')
            {
                return Result.Success();
            }

            return Result.Failure(Error.Validation("File is not a PDF."));
        }

        if (ext == ".txt" || contentType == "text/plain")
        {
            return Result.Success();
        }

        if (ext == ".docx"
            || contentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
        {
            // DOCX is a ZIP package (PK..)
            if (header.Length >= 2 && header[0] == (byte)'P' && header[1] == (byte)'K')
            {
                return Result.Success();
            }

            return Result.Failure(Error.Validation("File is not a DOCX document."));
        }

        return Result.Failure(Error.Validation("Unsupported file type."));
    }
}
