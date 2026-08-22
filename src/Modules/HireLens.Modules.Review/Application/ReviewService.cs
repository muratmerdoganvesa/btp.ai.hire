using HireLens.Contracts.Review;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Review.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Review.Application;

public interface IReviewService
{
    Task<Result<DecisionDto>> DecideAsync(Guid candidateId, RecordDecisionRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DecisionDto>>> ListAsync(Guid candidateId, CancellationToken cancellationToken);
}

public sealed class ReviewService(HireLensDbContext db, ITenantContext tenant, IClock clock) : IReviewService
{
    public async Task<Result<DecisionDto>> DecideAsync(
        Guid candidateId,
        RecordDecisionRequest request,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var created = Decision.Record(tenant.TenantId, candidateId, request.Outcome, request.Rationale, clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<DecisionDto>(created.Error);
        }

        db.Set<Decision>().Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(created.Value));
    }

    public async Task<Result<IReadOnlyList<DecisionDto>>> ListAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<Decision>().Where(d => d.CandidateId == candidateId).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<DecisionDto>>(rows.Select(ToDto).ToList());
    }

    private static DecisionDto ToDto(Decision decision) =>
        new(decision.Id, decision.CandidateId, decision.Outcome, decision.Rationale, decision.DecidedAt);
}
