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
using Microsoft.Extensions.Logging;

namespace HireLens.Modules.Documents.Application;

public sealed class ParseCvJob(
    HireLensDbContext db,
    IObjectStore objectStore,
    IFileGuard fileGuard,
    IPiiMasker masker,
    IAiGateway gateway,
    IAnalysisJobs jobs,
    IParseCache parseCache,
    IClock clock,
    ILogger<ParseCvJob> logger)
{
    public async Task RunAsync(Guid documentId, Guid jobId, CancellationToken cancellationToken)
    {
        var document = await db.Set<CvDocument>().SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        var job = await db.Set<AnalysisJob>().SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (document is null || job is null)
        {
            logger.LogWarning("Parse skipped: document or job missing (document={DocumentId}, job={JobId})", documentId, jobId);
            return;
        }

        job.Run(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        string masked;
        try
        {
            var parsed = await ExtractAndMaskAsync(document, cancellationToken);
            masked = parsed.MaskedText;
            document.MarkParsed(masked, parsed.FromCache ? "cache" : "01-cv-extraction@v1.1.0");
            if (!parsed.FromCache)
            {
                await TryStoreParseCacheAsync(parsed.ContentHash, masked, cancellationToken);
            }
            job.Succeed(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CV parse failed for document {DocumentId}", documentId);
            document.MarkFailed();
            job.Fail(ex.Message, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            jobs.EnqueueMatching(document.TenantId, document.Id);
        }
        catch (Exception matchEx)
        {
            logger.LogWarning(matchEx, "Matching enqueue failed for document {DocumentId}", documentId);
        }

        try
        {
            _ = await gateway.ExecuteAsync<CvExtractionResult>(
                AiTaskType.CvExtraction,
                new PromptContext(masked, "v1.1.0"),
                ct: cancellationToken);
        }
        catch (Exception advisoryEx)
        {
            logger.LogDebug(advisoryEx, "Advisory CV extraction LLM call failed for document {DocumentId}", documentId);
        }
    }

    private async Task<(string MaskedText, string ContentHash, bool FromCache)> ExtractAndMaskAsync(
        CvDocument document,
        CancellationToken cancellationToken)
    {
        await using var stream = await objectStore.GetAsync(document.ObjectKey, cancellationToken);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var scan = fileGuard.Scan(document.FileName, document.ContentType, bytes);
        if (scan.IsFailure)
        {
            throw new InvalidOperationException(scan.Error.Message);
        }

        var extraction = CvTextExtractor.Extract(document.FileName, document.ContentType, bytes);
        if (extraction.Status == ExtractionStatus.Unusable)
        {
            throw new InvalidOperationException(extraction.Reason ?? "Document text could not be extracted.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(extraction.Text)));
        var cached = await TryReadParseCacheAsync(hash, cancellationToken);
        if (cached is not null)
        {
            return (cached, hash, true);
        }

        return (masker.Mask(extraction.Text).Text, hash, false);
    }

    private async Task<string?> TryReadParseCacheAsync(string hash, CancellationToken cancellationToken)
    {
        try
        {
            return await parseCache.TryGetAsync(hash, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Parse cache read skipped");
            return null;
        }
    }

    private async Task TryStoreParseCacheAsync(string contentHash, string masked, CancellationToken cancellationToken)
    {
        try
        {
            await parseCache.PutAsync(contentHash, masked, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Parse cache write skipped");
        }
    }

    private sealed record CvExtractionResult(string? Status, string? Note);
}
