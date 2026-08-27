using System.Text;
using System.Security.Cryptography;
using HireLens.AiGateway;
using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Prompts;
using HireLens.AiGateway.Providers;
using HireLens.Contracts.Documents;
using HireLens.Contracts.Matching;
using HireLens.Infrastructure.Persistence;
using HireLens.Infrastructure.Storage;
using HireLens.Modules.Documents.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    IOptions<SapAiCoreOptions> aiCoreOptions,
    IPromptRegistry prompts,
    IHostEnvironment env,
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
            document.MarkParsed(masked, parsed.FromCache ? "cache" : "01-cv-extraction@v1");
            if (!parsed.FromCache)
            {
                await TryStoreParseCacheAsync(parsed.ContentHash, masked, cancellationToken);
            }

            var aiOk = await TryHostedCvExtractionAsync(masked, cancellationToken);
            if (!aiOk && !IsTesting)
            {
                document.MarkFailed();
                job.Fail("CV extraction AI yanıt vermedi veya parseQuality yetersiz.", clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                return;
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

    private async Task<bool> TryHostedCvExtractionAsync(string masked, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = prompts.Get("CvExtraction", "1.1.0");
            var deploymentId = string.IsNullOrWhiteSpace(aiCoreOptions.Value.CvExtractionDeploymentId)
                ? aiCoreOptions.Value.DeploymentId
                : aiCoreOptions.Value.CvExtractionDeploymentId;
            var result = await gateway.ExecuteAsync<string>(
                AiTaskType.CvExtraction,
                new PromptContext(
                    TaskInput: masked,
                    PromptVersion: prompt.Version,
                    Variables: new Dictionary<string, string>
                    {
                        ["cv_text"] = masked,
                        ["application_data"] = "yok"
                    },
                    SystemPrompt: prompt.SystemPrompt,
                    UserPrompt: prompt.UserTemplate,
                    DeploymentId: deploymentId),
                new AiOptions(MaxOutputTokens: 8000, Temperature: 0.1),
                cancellationToken);
            var usable = CvExtractionMapper.IsUsable(result.Value);
            if (!usable)
            {
                logger.LogWarning(
                    "CV extraction JSON was not usable. Preview={Preview}",
                    Truncate(result.Value));
            }

            return usable;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CV extraction orchestration call failed");
            return false;
        }
    }

    private bool IsTesting =>
        string.Equals(env.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty)";
        }

        return value.Length <= 800 ? value : value[..800] + "…";
    }
}
