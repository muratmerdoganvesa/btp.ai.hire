using HireLens.Contracts.Candidates;
using HireLens.Contracts.Documents;
using HireLens.Contracts.Privacy;
using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Documents.Application;
using HireLens.Modules.Documents.Domain;
using HireLens.Modules.Recruiting.Domain;
using HireLens.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Api.Application;

public interface IPublicApplicationService
{
    Task<Result<PublicJobDto>> GetJobAsync(string slug, CancellationToken cancellationToken);

    Task<Result<PublicApplicationResponse>> ApplyAsync(
        PublicApplicationRequest request,
        IFormFile? cv,
        string? clientIp,
        CancellationToken cancellationToken);

    Task<Result<PublicApplicationStatusDto>> GetStatusAsync(string reference, CancellationToken cancellationToken);

    Task<Result<PublicApplicationResponse>> ReuploadCvAsync(
        string reference,
        IFormFile cv,
        CancellationToken cancellationToken);
}

public sealed class PublicApplicationService(
    HireLensDbContext db,
    SystemTenantScope systemScope,
    ICandidateWritePort candidates,
    IDocumentService documents,
    IPrivacyConsentPort privacy) : IPublicApplicationService
{
    public const string ApplicationConsentPurpose = "candidate_application_v1";
    public const string ConsentTextVersion = "2026-08-01";
    private const long MaxBytes = 10 * 1024 * 1024;

    public async Task<Result<PublicJobDto>> GetJobAsync(string slug, CancellationToken cancellationToken)
    {
        var position = await ResolvePositionAsync(slug, cancellationToken);
        if (position is null)
        {
            return Result.Failure<PublicJobDto>(Error.NotFound("Job was not found."));
        }

        return Result.Success(new PublicJobDto(
            position.Id,
            DisplaySlug(position),
            position.Title,
            position.JobDescription,
            position.Criteria.Select(c => new PositionCriterionDto(c.Id, c.Name, c.Description, c.Weight)).ToList(),
            IsOpen: true));
    }

    public async Task<Result<PublicApplicationResponse>> ApplyAsync(
        PublicApplicationRequest request,
        IFormFile? cv,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        if (!request.ConsentAccepted)
        {
            return Result.Failure<PublicApplicationResponse>(Error.Validation("Consent is required to apply."));
        }

        if (!string.Equals(request.ConsentVersion, ConsentTextVersion, StringComparison.Ordinal))
        {
            return Result.Failure<PublicApplicationResponse>(Error.Validation("Consent text version is outdated."));
        }

        if (cv is null || cv.Length == 0)
        {
            return Result.Failure<PublicApplicationResponse>(Error.Validation("CV file is required."));
        }

        if (cv.Length > MaxBytes)
        {
            return Result.Failure<PublicApplicationResponse>(Error.Validation("CV must be 10 MB or smaller."));
        }

        var position = await ResolvePositionAsync(request.Slug, cancellationToken);
        if (position is null)
        {
            return Result.Failure<PublicApplicationResponse>(Error.NotFound("Job was not found."));
        }

        using (systemScope.Use(position.TenantId, "public-apply"))
        {
            var displayName = BuildDisplayName(request);
            var created = await candidates.CreateAsync(
                position.Id,
                new CreateCandidateRequest(displayName),
                cancellationToken);
            if (created.IsFailure)
            {
                return Result.Failure<PublicApplicationResponse>(created.Error);
            }

            await privacy.GrantAsync(
                created.Value.Id,
                ApplicationConsentPurpose,
                ConsentTextVersion,
                clientIp,
                cancellationToken);

            var upload = await UploadCvAsync(created.Value.Id, position.Id, cv, cancellationToken);
            if (upload.IsFailure)
            {
                return Result.Failure<PublicApplicationResponse>(upload.Error);
            }

            var reference = ToReference(created.Value.Id);
            return Result.Success(new PublicApplicationResponse(
                created.Value.Id,
                reference,
                upload.Value.DocumentId,
                upload.Value.UploadUrl,
                upload.Value.Method));
        }
    }

    public async Task<Result<PublicApplicationStatusDto>> GetStatusAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        var candidateId = await ResolveCandidateIdAsync(reference, cancellationToken);
        if (candidateId is null)
        {
            return Result.Failure<PublicApplicationStatusDto>(Error.NotFound("Application was not found."));
        }

        var candidate = await db.Set<HireLens.Modules.Candidate.Domain.Candidate>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == candidateId.Value, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure<PublicApplicationStatusDto>(Error.NotFound("Application was not found."));
        }

        var document = await db.Set<CvDocument>()
            .IgnoreQueryFilters()
            .Where(d => d.CandidateId == candidateId.Value)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var stage = ResolvePublicStage(document);
        return Result.Success(new PublicApplicationStatusDto(
            ToReference(candidate.Id),
            candidate.Id,
            stage,
            stage is "document_unreadable"));
    }

    public async Task<Result<PublicApplicationResponse>> ReuploadCvAsync(
        string reference,
        IFormFile cv,
        CancellationToken cancellationToken)
    {
        if (cv.Length <= 0 || cv.Length > MaxBytes)
        {
            return Result.Failure<PublicApplicationResponse>(Error.Validation("CV must be between 1 byte and 10 MB."));
        }

        var candidateId = await ResolveCandidateIdAsync(reference, cancellationToken);
        if (candidateId is null)
        {
            return Result.Failure<PublicApplicationResponse>(Error.NotFound("Application was not found."));
        }

        var candidate = await db.Set<HireLens.Modules.Candidate.Domain.Candidate>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == candidateId.Value, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure<PublicApplicationResponse>(Error.NotFound("Application was not found."));
        }

        using (systemScope.Use(candidate.TenantId, "public-reupload"))
        {
            var upload = await UploadCvAsync(candidate.Id, candidate.PositionId, cv, cancellationToken);
            if (upload.IsFailure)
            {
                return Result.Failure<PublicApplicationResponse>(upload.Error);
            }

            return Result.Success(new PublicApplicationResponse(
                candidate.Id,
                ToReference(candidate.Id),
                upload.Value.DocumentId,
                upload.Value.UploadUrl,
                upload.Value.Method));
        }
    }

    private async Task<Result<UploadSessionDto>> UploadCvAsync(
        Guid candidateId,
        Guid positionId,
        IFormFile cv,
        CancellationToken cancellationToken)
    {
        var fileName = string.IsNullOrWhiteSpace(cv.FileName) ? "cv.pdf" : cv.FileName;
        var contentType = string.IsNullOrWhiteSpace(cv.ContentType) ? "application/octet-stream" : cv.ContentType;
        var session = await documents.StartUploadAsync(
            candidateId,
            positionId,
            new UploadSessionRequest(fileName, contentType, cv.Length),
            cancellationToken);
        if (session.IsFailure)
        {
            return Result.Failure<UploadSessionDto>(session.Error);
        }

        await using var stream = cv.OpenReadStream();
        var stored = await documents.StoreBytesAsync(
            ExtractObjectKey(session.Value.UploadUrl),
            stream,
            contentType,
            cancellationToken);
        if (stored.IsFailure)
        {
            return Result.Failure<UploadSessionDto>(stored.Error);
        }

        var completed = await documents.CompleteAsync(session.Value.DocumentId, cancellationToken);
        if (completed.IsFailure)
        {
            return Result.Failure<UploadSessionDto>(completed.Error);
        }

        return session;
    }

    private static string ExtractObjectKey(string uploadUrl)
    {
        var marker = "/objects/";
        var index = uploadUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return uploadUrl.TrimStart('/');
        }

        return Uri.UnescapeDataString(uploadUrl[(index + marker.Length)..]);
    }

    private static string BuildDisplayName(PublicApplicationRequest request) =>
        $"{request.DisplayName.Trim()} <{request.Email.Trim()}>";

    private static string ToReference(Guid candidateId) =>
        candidateId.ToString("N")[..8].ToUpperInvariant();

    private async Task<Guid?> ResolveCandidateIdAsync(string reference, CancellationToken cancellationToken)
    {
        reference = reference.Trim().ToUpperInvariant();
        if (reference.Length != 8)
        {
            return null;
        }

        var matches = await db.Set<HireLens.Modules.Candidate.Domain.Candidate>()
            .IgnoreQueryFilters()
            .Where(c => c.Id.ToString().Replace("-", "").StartsWith(reference))
            .Take(2)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<Position?> ResolvePositionAsync(string slug, CancellationToken cancellationToken)
    {
        slug = slug.Trim();
        if (Guid.TryParse(slug, out var id))
        {
            return await db.Set<Position>()
                .IgnoreQueryFilters()
                .Include(p => p.Criteria)
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        return await db.Set<Position>()
            .IgnoreQueryFilters()
            .Include(p => p.Criteria)
            .SingleOrDefaultAsync(p => p.Slug == slug, cancellationToken);
    }

    private static string DisplaySlug(Position position) =>
        string.IsNullOrWhiteSpace(position.Slug)
            ? Position.BuildSlug(position.Title, position.Id)
            : position.Slug;

    private static string ResolvePublicStage(CvDocument? document)
    {
        if (document is null)
        {
            return "received";
        }

        return document.Status switch
        {
            "failed" => "document_unreadable",
            "parsed" => "processing",
            "uploaded" => "processing",
            _ => "received"
        };
    }
}
