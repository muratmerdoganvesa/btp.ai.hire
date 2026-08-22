using HireLens.Contracts.Evidence;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Evidence.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Evidence.Application;

public sealed class EvidenceScoring(HireLensDbContext db, ITenantContext tenant) : IEvidenceScoring
{
    public async Task ApplyAsync(
        Guid evaluationId,
        IReadOnlyList<ProposedCriterionScore> proposals,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);

        foreach (var proposal in proposals)
        {
            var drafts = proposal.Evidence
                .Select(e => new EvidenceDraft(e.Source, e.Quote, e.StartOffset, e.EndOffset))
                .ToList();

            var score = CriterionScore.Create(
                tenant.TenantId,
                evaluationId,
                proposal.CriterionId,
                proposal.Weight,
                proposal.Score,
                proposal.Confidence,
                drafts);

            db.Set<CriterionScore>().Add(score);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CriterionScoreDto>> ListForEvaluationAsync(
        Guid evaluationId,
        IReadOnlyDictionary<Guid, string> criterionNames,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<CriterionScore>()
            .Where(s => s.EvaluationId == evaluationId)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new CriterionScoreDto(
            row.CriterionId,
            criterionNames.GetValueOrDefault(row.CriterionId, "criterion"),
            row.Score,
            row.Weight,
            row.Confidence,
            row.EvidenceStatus,
            row.Evidence.Select(e => new EvidenceDto(e.Source, e.Quote, e.StartOffset, e.EndOffset)).ToList()
        )).ToList();
    }
}
