using HireLens.Contracts.Candidates;
using HireLens.Contracts.Compliance;
using HireLens.Contracts.Matching;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Compliance.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Compliance.Application;

public interface IComplianceService
{
    Task<Result<CandidateExportDto>> ExportAsync(Guid candidateId, CancellationToken cancellationToken);

    Task<Result<DataDeletionRequestDto>> RequestDeletionAsync(CreateDeletionRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DataDeletionRequestDto>>> ListDeletionsAsync(CancellationToken cancellationToken);
}

public sealed class ComplianceService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock,
    ICandidateReadPort candidates,
    IEvaluationReadPort evaluations) : IComplianceService
{
    public async Task<Result<CandidateExportDto>> ExportAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var candidate = await candidates.GetAsync(candidateId, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure<CandidateExportDto>(Error.NotFound("Candidate was not found."));
        }

        var evaluation = await evaluations.GetForCandidateAsync(candidateId, cancellationToken);
        return Result.Success(new CandidateExportDto(
            candidate.Id,
            candidate.DisplayName,
            new { candidate, evaluation },
            clock.UtcNow));
    }

    public async Task<Result<DataDeletionRequestDto>> RequestDeletionAsync(
        CreateDeletionRequest request,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var candidate = await candidates.GetAsync(request.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure<DataDeletionRequestDto>(Error.NotFound("Candidate was not found."));
        }

        var row = DataDeletionRequest.Open(tenant.TenantId, request.CandidateId, request.Reason, clock.UtcNow);
        db.Set<DataDeletionRequest>().Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new DataDeletionRequestDto(row.Id, row.CandidateId, row.Status, row.RequestedAt));
    }

    public async Task<Result<IReadOnlyList<DataDeletionRequestDto>>> ListDeletionsAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<DataDeletionRequest>().ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<DataDeletionRequestDto>>(
            rows.Select(r => new DataDeletionRequestDto(r.Id, r.CandidateId, r.Status, r.RequestedAt)).ToList());
    }
}
