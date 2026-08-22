using HireLens.Contracts.Documents;
using HireLens.Contracts.Matching;
using HireLens.Infrastructure.Persistence;
using HireLens.Infrastructure.Storage;
using HireLens.Modules.Documents.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Documents.Application;

public interface IDocumentService
{
    Task<Result<UploadSessionDto>> StartUploadAsync(Guid candidateId, Guid positionId, UploadSessionRequest request, CancellationToken cancellationToken);

    Task<Result> StoreBytesAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken);

    Task<Result<JobStatusDto>> CompleteAsync(Guid documentId, CancellationToken cancellationToken);

    Task<Result<JobStatusDto>> GetJobAsync(Guid jobId, CancellationToken cancellationToken);
}

public sealed class DocumentService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock,
    IObjectStore objectStore,
    IAnalysisJobs jobs) : IDocumentService, IDocumentTextPort
{
    public async Task<Result<UploadSessionDto>> StartUploadAsync(
        Guid candidateId,
        Guid positionId,
        UploadSessionRequest request,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var created = CvDocument.Create(
            tenant.TenantId,
            candidateId,
            positionId,
            request.FileName,
            request.ContentType,
            request.SizeBytes,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<UploadSessionDto>(created.Error);
        }

        db.Set<CvDocument>().Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        var url = await objectStore.CreateUploadUrlAsync(created.Value.ObjectKey, request.ContentType, cancellationToken);
        return Result.Success(new UploadSessionDto(created.Value.Id, url, "PUT"));
    }

    public async Task<Result> StoreBytesAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var document = await db.Set<CvDocument>().SingleOrDefaultAsync(d => d.ObjectKey == objectKey, cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("Upload target was not found."));
        }

        await objectStore.PutAsync(objectKey, content, contentType, cancellationToken);
        document.MarkUploaded();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<JobStatusDto>> CompleteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var document = await db.Set<CvDocument>().SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure<JobStatusDto>(Error.NotFound("Document was not found."));
        }

        var job = AnalysisJob.Queue(tenant.TenantId, "parse", document.Id, clock.UtcNow);
        db.Set<AnalysisJob>().Add(job);
        await db.SaveChangesAsync(cancellationToken);
        jobs.EnqueueDocumentParse(tenant.TenantId, document.Id);
        return Result.Success(ToDto(job));
    }

    public async Task<Result<JobStatusDto>> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var job = await db.Set<AnalysisJob>().SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        return job is null
            ? Result.Failure<JobStatusDto>(Error.NotFound("Job was not found."))
            : Result.Success(ToDto(job));
    }

    public async Task<DocumentTextSnapshot?> GetMaskedTextAsync(Guid documentId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var document = await db.Set<CvDocument>().SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document?.MaskedText is null)
        {
            return null;
        }

        return new DocumentTextSnapshot(document.Id, document.CandidateId, document.PositionId, document.MaskedText);
    }

    private static JobStatusDto ToDto(AnalysisJob job) =>
        new(job.Id, job.Kind, job.Status, job.Error, job.UpdatedAt);
}
