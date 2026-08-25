using HireLens.Contracts.Candidates;
using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Candidate.Domain;
using HireLens.Modules.Matching.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Api.Application;

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

        var candidates = await db.Set<Candidate>()
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

public sealed class CandidateEvaluationSummaryService(HireLensDbContext db, ITenantContext tenant)
    : ICandidateEvaluationSummaryPort
{
    public async Task<IReadOnlyDictionary<Guid, CandidateEvaluationSummary>> GetForCandidatesAsync(
        IReadOnlyList<Guid> candidateIds,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        if (candidateIds.Count == 0)
        {
            return new Dictionary<Guid, CandidateEvaluationSummary>();
        }

        var evaluations = await db.Set<Evaluation>()
            .Where(e => candidateIds.Contains(e.CandidateId))
            .ToListAsync(cancellationToken);

        var latest = evaluations
            .GroupBy(e => e.CandidateId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First());

        return candidateIds.ToDictionary(
            id => id,
            id => latest.TryGetValue(id, out var evaluation)
                ? ToSummary(evaluation)
                : new CandidateEvaluationSummary(null, null, null, 0, "processing"));
    }

    private static CandidateEvaluationSummary ToSummary(Evaluation evaluation)
    {
        var riskCount = string.IsNullOrWhiteSpace(evaluation.NeedsVerificationJson)
            ? 0
            : evaluation.NeedsVerificationJson.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

        var recommended = ResolveRecommendedAction(
            evaluation.Status,
            evaluation.OverallScore,
            evaluation.CoverageRatio,
            riskCount);

        return new CandidateEvaluationSummary(
            evaluation.OverallScore,
            evaluation.CoverageRatio,
            evaluation.Status,
            riskCount,
            recommended);
    }

    private static string ResolveRecommendedAction(string status, int? score, decimal coverage, int riskCount)
    {
        if (status is "failed")
        {
            return "error";
        }

        if (status is not "completed")
        {
            return "processing";
        }

        if (riskCount > 0 || coverage < 0.5m)
        {
            return "request_info";
        }

        if (score is >= 75 && coverage >= 0.6m)
        {
            return "shortlist";
        }

        return "review";
    }
}
