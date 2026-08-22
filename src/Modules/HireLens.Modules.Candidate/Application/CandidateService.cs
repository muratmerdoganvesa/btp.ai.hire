using HireLens.Contracts.Candidates;
using HireLens.Infrastructure.Persistence;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Candidate.Application;

public interface ICandidateService
{
    Task<Result<IReadOnlyList<CandidateDto>>> ListAsync(Guid positionId, CancellationToken cancellationToken);

    Task<Result<CandidateDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<CandidateDto>> CreateAsync(Guid positionId, CreateCandidateRequest request, CancellationToken cancellationToken);
}

public sealed class CandidateService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock) : ICandidateService, ICandidateReadPort, ICandidateWritePort
{
    public async Task<Result<IReadOnlyList<CandidateDto>>> ListAsync(Guid positionId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<Domain.Candidate>()
            .Where(c => c.PositionId == positionId)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<CandidateDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<Result<CandidateDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var row = await db.Set<Domain.Candidate>().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        return row is null
            ? Result.Failure<CandidateDto>(Error.NotFound("Candidate was not found."))
            : Result.Success(ToDto(row));
    }

    public async Task<Result<CandidateDto>> CreateAsync(
        Guid positionId,
        CreateCandidateRequest request,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var created = Domain.Candidate.Create(tenant.TenantId, positionId, request.DisplayName, clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<CandidateDto>(created.Error);
        }

        db.Set<Domain.Candidate>().Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(created.Value));
    }

    async Task<CandidateSnapshot?> ICandidateReadPort.GetAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        var result = await GetAsync(candidateId, cancellationToken);
        return result.IsFailure
            ? null
            : new CandidateSnapshot(result.Value.Id, result.Value.PositionId, result.Value.DisplayName);
    }

    private static CandidateDto ToDto(Domain.Candidate candidate) =>
        new(candidate.Id, candidate.PositionId, candidate.DisplayName, null, null, candidate.Status, candidate.CreatedAt);
}
