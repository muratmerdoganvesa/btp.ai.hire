using System.Text;
using System.Security.Cryptography;
using HireLens.AiGateway;
using HireLens.AiGateway.Masking;
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

            var extraction = CvTextExtractor.Extract(document.FileName, document.ContentType, bytes);
            if (extraction.Status == ExtractionStatus.Unusable)
            {
                document.MarkFailed();
                job.Fail(extraction.Reason ?? "Document text could not be extracted.", clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(extraction.Text)));
            var cached = await parseCache.TryGetAsync(hash, cancellationToken);
            var masked = cached ?? masker.Mask(extraction.Text).Text;
            document.MarkParsed(masked, cached is null ? "01-cv-extraction@v1.1.0" : "cache");
            if (cached is null)
            {
                await parseCache.PutAsync(hash, masked, cancellationToken);
            }

            // Structured extraction is logged via gateway; profile persistence lands in a later slice.
            _ = await gateway.ExecuteAsync<CvExtractionResult>(
                AiTaskType.CvExtraction,
                new PromptContext(masked, "v1.1.0"),
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

    private sealed record CvExtractionResult(string? Status, string? Note);
}
