using HireLens.SharedKernel;

namespace HireLens.Modules.Documents.Domain;

public sealed class CvDocument : ITenantEntity
{
    private CvDocument()
    {
        ObjectKey = string.Empty;
        ContentType = string.Empty;
        FileName = string.Empty;
        Status = "pending_upload";
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CandidateId { get; private set; }

    public Guid PositionId { get; private set; }

    public string ObjectKey { get; private set; }

    public string ContentType { get; private set; }

    public string FileName { get; private set; }

    public long SizeBytes { get; private set; }

    public string Status { get; private set; }

    public string? MaskedText { get; private set; }

    public string? PromptVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<CvDocument> Create(
        Guid tenantId,
        Guid candidateId,
        Guid positionId,
        string fileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset createdAt)
    {
        if (!IsAllowed(contentType, fileName))
        {
            return Result.Failure<CvDocument>(Error.Validation("Only PDF, DOCX, and plain-text CVs are accepted."));
        }

        if (sizeBytes <= 0 || sizeBytes > 10 * 1024 * 1024)
        {
            return Result.Failure<CvDocument>(Error.Validation("CV must be between 1 byte and 10 MB."));
        }

        var id = Guid.NewGuid();
        return Result.Success(new CvDocument
        {
            Id = id,
            TenantId = tenantId,
            CandidateId = candidateId,
            PositionId = positionId,
            FileName = fileName.Trim(),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            ObjectKey = $"{tenantId:N}/{id:N}/{fileName.Trim()}",
            Status = "pending_upload",
            CreatedAt = createdAt
        });
    }

    public void MarkUploaded() => Status = "uploaded";

    public void MarkParsed(string maskedText, string promptVersion)
    {
        MaskedText = maskedText;
        PromptVersion = promptVersion;
        Status = "parsed";
    }

    public void MarkFailed() => Status = "failed";

    private static bool IsAllowed(string contentType, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return contentType is "application/pdf" or "text/plain"
                   or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
               || ext is ".pdf" or ".txt" or ".docx";
    }
}

public sealed class AnalysisJob : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Kind { get; private set; } = string.Empty;

    public string Status { get; private set; } = "queued";

    public string? Error { get; private set; }

    public Guid? DocumentId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static AnalysisJob Queue(Guid tenantId, string kind, Guid documentId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = kind,
            Status = "queued",
            DocumentId = documentId,
            UpdatedAt = now
        };

    public void Succeed(DateTimeOffset now)
    {
        Status = "succeeded";
        UpdatedAt = now;
        Error = null;
    }

    public void Fail(string error, DateTimeOffset now)
    {
        Status = "failed";
        Error = error;
        UpdatedAt = now;
    }

    public void Run(DateTimeOffset now)
    {
        Status = "running";
        UpdatedAt = now;
    }
}
