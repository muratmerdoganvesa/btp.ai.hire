using HireLens.AiGateway;
using HireLens.Contracts.Documents;
using HireLens.Contracts.Evidence;
using HireLens.Contracts.Matching;
using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Matching.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Matching.Application;

public interface IEvaluationService : IEvaluationReadPort, IEvaluationBlendPort
{
}

public sealed class MatchingJob(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock,
    IPositionReadPort positions,
    IDocumentTextPort documents,
    IEvidenceScoring evidence,
    IAiGateway gateway) : IEvaluationService
{
    public async Task RunAsync(Guid documentId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var text = await documents.GetMaskedTextAsync(documentId, cancellationToken);
        if (text is null)
        {
            return;
        }

        var position = await positions.GetAsync(text.PositionId, cancellationToken);
        if (position is null)
        {
            return;
        }

        var evaluation = await db.Set<Evaluation>()
            .SingleOrDefaultAsync(e => e.DocumentId == documentId, cancellationToken)
            ?? Evaluation.Start(tenant.TenantId, text.PositionId, text.CandidateId, documentId, clock.UtcNow);

        if (evaluation.Status == "queued")
        {
            db.Set<Evaluation>().Add(evaluation);
            await db.SaveChangesAsync(cancellationToken);
        }

        var proposals = DeterministicMatcher.Score(text.MaskedText, position);
        _ = await gateway.ExecuteAsync<MatchStub>(
            AiTaskType.JdCvMatching,
            new PromptContext($"{position.JobDescription}\n---\n{text.MaskedText}", "v1"),
            ct: cancellationToken);

        await evidence.ApplyAsync(evaluation.Id, proposals, cancellationToken);
        var overall = DeterministicMatcher.Overall(proposals);
        var gaps = proposals.Where(p => p.Score is null).Select(p => p.CriterionId.ToString()).ToList();
        evaluation.Complete(
            overall,
            "v1",
            overall is null ? "Insufficient evidence for an overall score." : "Evidence-bound scores are ready for human review.",
            gaps.Count == 0 ? [] : ["Ask for evidence on criteria still marked insufficient."],
            gaps);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<EvaluationDto?> GetForCandidateAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var evaluation = await db.Set<Evaluation>()
            .Where(e => e.CandidateId == candidateId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (evaluation is null)
        {
            return null;
        }

        var position = await positions.GetAsync(evaluation.PositionId, cancellationToken);
        var names = position?.Criteria.ToDictionary(c => c.Id, c => c.Name)
            ?? new Dictionary<Guid, string>();
        var scores = await evidence.ListForEvaluationAsync(evaluation.Id, names, cancellationToken);

        return new EvaluationDto(
            evaluation.Id,
            evaluation.PositionId,
            evaluation.CandidateId,
            evaluation.OverallScore,
            evaluation.Status,
            evaluation.PromptVersion,
            evaluation.Summary,
            Split(evaluation.FollowUpsJson),
            Split(evaluation.NeedsVerificationJson),
            scores);
    }

    public async Task BlendInterviewAsync(
        Guid candidateId,
        int? interviewScore,
        int interviewWeight,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var evaluation = await db.Set<Evaluation>()
            .Where(e => e.CandidateId == candidateId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (evaluation is null)
        {
            return;
        }

        evaluation.BlendInterview(interviewScore, interviewWeight);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private sealed record MatchStub(string? Status);
}
