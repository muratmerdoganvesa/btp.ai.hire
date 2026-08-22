using System.Text;
using HireLens.AiGateway;
using HireLens.AiGateway.Masking;
using System.Security.Cryptography;
using HireLens.Contracts.Documents;
using HireLens.Contracts.Matching;
using HireLens.Infrastructure.Persistence;
using HireLens.Infrastructure.Storage;
using HireLens.Modules.Documents.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Documents.Application;

public sealed class ParseCvJob(
    HireLensDbContext db,
    IObjectStore objectStore,
    IFileGuard fileGuard,
    IPiiMasker masker,
    IAiGateway gateway,
    IAnalysisJobs jobs,
    IParseCache parseCache,
    IClock clock)
{
    public async Task RunAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.Set<CvDocument>().SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        var job = await db.Set<AnalysisJob>().SingleOrDefaultAsync(j => j.DocumentId == documentId && j.Kind == "parse", cancellationToken);
        if (document is null || job is null)
        {
            return;
        }

        job.Run(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await using var stream = await objectStore.GetAsync(document.ObjectKey, cancellationToken);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();
            var scan = fileGuard.Scan(document.FileName, document.ContentType, bytes);
            if (scan.IsFailure)
            {
                document.MarkFailed();
                job.Fail(scan.Error.Message, clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var cached = await parseCache.TryGetAsync(hash, cancellationToken);
            var raw = ExtractText(document.ContentType, document.FileName, bytes);
            var masked = cached ?? masker.Mask(raw).Text;
            document.MarkParsed(masked, cached is null ? "v1" : "cache");
            if (cached is null)
            {
                await parseCache.PutAsync(hash, masked, cancellationToken);
            }

            _ = await gateway.ExecuteAsync<CvExtractionResult>(
                AiTaskType.CvExtraction,
                new PromptContext(masked, "v1"),
                ct: cancellationToken);

            job.Succeed(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            jobs.EnqueueMatching(document.TenantId, document.Id);
        }
        catch (Exception ex)
        {
            document.MarkFailed();
            job.Fail(ex.Message, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string ExtractText(string contentType, string fileName, byte[] bytes)
    {
        if (contentType == "text/plain" || Path.GetExtension(fileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private sealed record CvExtractionResult(string? Status, string? Note);
}
