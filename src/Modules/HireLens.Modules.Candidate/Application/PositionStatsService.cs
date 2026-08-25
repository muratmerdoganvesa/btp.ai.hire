using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Matching.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Candidate.Application;

public sealed class PositionStatsService(HireLensDbContext db, ITenantContext tenant) : IPositionStatsPort
{
    public async Task<IReadOnlyDictionary<Guid, PositionStatsDto>> GetForPositionsAsync(
        IReadOnlyList<Guid> positionIds,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        if (positionIds.Count == 0)
        {
            return new Dictionary<Guid, PositionStatsDto>();
        }

        var candidates = await db.Set<Domain.Candidate>()
            .Where(c => positionIds.Contains(c.PositionId))
            .ToListAsync(cancellationToken);
        var evaluations = await db.Set<Evaluation>()
            .Where(e => positionIds.Contains(e.PositionId))
            .ToListAsync(cancellationToken);

        return positionIds.ToDictionary(
            id => id,
            id =>
            {
                var posCandidates = candidates.Where(c => c.PositionId == id).ToList();
                var posEvals = evaluations.Where(e => e.PositionId == id).ToList();
                return new PositionStatsDto(
                    posCandidates.Count,
                    posEvals.Count(e => e.Status is "completed"),
                    posCandidates.Count(c => c.Status is "received" or "analyzing"),
                    posEvals.Count(e => e.Status is "failed"),
                    posEvals.Count(e => e.Status is "completed" && e.CoverageRatio < 0.5m));
            });
    }
}
