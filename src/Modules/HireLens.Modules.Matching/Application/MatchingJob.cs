using HireLens.AiGateway;
using HireLens.Contracts.Documents;
using HireLens.Contracts.Evidence;
using HireLens.Contracts.Matching;
using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Matching.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using HireLens.AiGateway.Providers;

namespace HireLens.Modules.Matching.Application;

public interface IEvaluationService : IEvaluationReadPort, IEvaluationBlendPort, IEvaluationWritePort
{
}

/// <summary>
/// End-to-end CV evaluation: match → ScoreCalculator → evidence persist → summary.
/// LLM produces criterion scores and evidence only; the total is computed in C#.
/// </summary>
public sealed class MatchingJob(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock,
    IPositionReadPort positions,
    IDocumentTextPort documents,
    IEvidenceScoring evidence,
    IAiGateway gateway,
    IOptions<SapAiCoreOptions> aiOptions,
    IAnalysisJobs jobs,
    IHostEnvironment env) : IEvaluationService
{
    public async Task RunAsync(Guid documentId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        try
        {
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

            if (evaluation.Status is "pending" or "Pending" or "queued")
            {
                if (db.Entry(evaluation).State == EntityState.Detached)
                {
                    db.Set<Evaluation>().Add(evaluation);
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            await RunEvaluationCoreAsync(evaluation, text.MaskedText, position, cancellationToken);
        }
        catch (Exception ex)
        {
            var evaluation = await db.Set<Evaluation>()
                .SingleOrDefaultAsync(e => e.DocumentId == documentId, cancellationToken);
            if (evaluation is not null)
            {
                evaluation.Fail("matching", ex.Message, clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public async Task RunEvaluationAsync(Guid evaluationId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var evaluation = await db.Set<Evaluation>()
            .SingleOrDefaultAsync(e => e.Id == evaluationId, cancellationToken);
        if (evaluation is null)
        {
            return;
        }

        var text = await documents.GetMaskedTextAsync(evaluation.DocumentId, cancellationToken);
        var position = await positions.GetAsync(evaluation.PositionId, cancellationToken);
        if (text is null || position is null)
        {
            evaluation.Fail("Matching", "Document or position not found.", clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        await RunEvaluationCoreAsync(evaluation, text.MaskedText, position, cancellationToken);
    }

    private async Task RunEvaluationCoreAsync(
        Evaluation evaluation,
        string maskedText,
        PositionSnapshot position,
        CancellationToken cancellationToken)
    {
        try
        {
            evaluation.SetStage("matching");
            await db.SaveChangesAsync(cancellationToken);

            IReadOnlyList<ProposedCriterionScore> proposals;
            try
            {
                var deploymentId = string.IsNullOrWhiteSpace(aiOptions.Value.MatchingDeploymentId)
                    ? aiOptions.Value.DeploymentId
                    : aiOptions.Value.MatchingDeploymentId;
                var jobDescription = BuildJobDescription(position);
                var aiResult = await gateway.ExecuteAsync<string>(
                    AiTaskType.JdCvMatching,
                    new PromptContext(
                        TaskInput: maskedText,
                        PromptVersion: "1",
                        Variables: new Dictionary<string, string>
                        {
                            ["job_description"] = jobDescription,
                            ["cv_text"] = maskedText
                        },
                        PlaceholdersOnly: true,
                        DeploymentId: deploymentId),
                    new AiOptions(MaxOutputTokens: 2048, Temperature: 0.1),
                    cancellationToken);
                var mapped = CriteriaMatchingMapper.TryMap(aiResult.Value, position);
                if (mapped is null)
                {
                    throw new InvalidOperationException("Eşleştirme AI geçerli skor döndürmedi.");
                }

                proposals = mapped;
            }
            catch (Exception ex) when (!IsTesting)
            {
                evaluation.Fail("matching", ex.Message, clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch
            {
                proposals = DeterministicMatcher.Score(maskedText, position);
            }

            evaluation.SetStage("scoring");
            await db.SaveChangesAsync(cancellationToken);

            await evidence.ApplyAsync(evaluation.Id, proposals, cancellationToken);
            var score = DeterministicMatcher.ToScoreResult(proposals, "position-weights-v1");
            var overall = score.Total is null ? (int?)null : (int)Math.Round(score.Total.Value);
            var gaps = score.SkippedCriteria.ToList();

            string summary;
            try
            {
                var summaryResult = await gateway.ExecuteAsync<SummaryStub>(
                    AiTaskType.RecruiterSummary,
                    new PromptContext(
                        $"score={overall};coverage={score.CoverageRatio};gaps={gaps.Count}",
                        "v1.0.0"),
                    ct: cancellationToken);
                summary = string.IsNullOrWhiteSpace(summaryResult.Value.Summary)
                    ? (overall is null
                        ? "Insufficient evidence for an overall score."
                        : "Evidence-bound scores are ready for human review.")
                    : summaryResult.Value.Summary!;
            }
            catch
            {
                summary = overall is null
                    ? "Insufficient evidence for an overall score."
                    : "Evidence-bound scores are ready for human review.";
            }

            var followUps = (position.InterviewQuestions ?? [])
                .Select(q => q.Question)
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Take(5)
                .ToList();

            var opts = aiOptions.Value;
            evaluation.Complete(
                overall,
                score.CoverageRatio,
                promptVersion: "02-criteria-matching@v1",
                rubricVersion: score.RubricVersion,
                modelName: string.IsNullOrWhiteSpace(opts.ModelName) ? "deterministic" : opts.ModelName,
                modelVersion: opts.ModelVersion,
                summary,
                followUps,
                gaps,
                clock.UtcNow);

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            evaluation.Fail(evaluation.Status, ex.Message, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<Guid> StartAsync(Guid candidateId, Guid jobDescriptionId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);

        var document = await documents.GetLatestParsedAsync(candidateId, jobDescriptionId, cancellationToken);
        if (document is null)
        {
            throw new InvalidOperationException("A parsed CV is required before starting an evaluation.");
        }

        var evaluation = Evaluation.Start(
            tenant.TenantId,
            jobDescriptionId,
            candidateId,
            document.DocumentId,
            clock.UtcNow);
        db.Set<Evaluation>().Add(evaluation);
        await db.SaveChangesAsync(cancellationToken);

        jobs.EnqueueEvaluation(tenant.TenantId, evaluation.Id);
        return evaluation.Id;
    }

    public async Task<EvaluationDto?> GetForCandidateAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var evaluation = await db.Set<Evaluation>()
            .Where(e => e.CandidateId == candidateId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return evaluation is null ? null : await ToDtoAsync(evaluation, cancellationToken);
    }

    public async Task<EvaluationDto?> GetByIdAsync(Guid evaluationId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var evaluation = await db.Set<Evaluation>()
            .SingleOrDefaultAsync(e => e.Id == evaluationId, cancellationToken);
        return evaluation is null ? null : await ToDtoAsync(evaluation, cancellationToken);
    }

    public async Task<EvaluationAuditDto?> GetAuditAsync(Guid evaluationId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var evaluation = await db.Set<Evaluation>()
            .SingleOrDefaultAsync(e => e.Id == evaluationId, cancellationToken);
        if (evaluation is null)
        {
            return null;
        }

        return new EvaluationAuditDto(
            evaluation.Id,
            evaluation.PromptVersion,
            evaluation.RubricVersion,
            evaluation.ModelName,
            evaluation.ModelVersion,
            evaluation.CoverageRatio,
            evaluation.ExecutedAt,
            evaluation.Status);
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

    private async Task<EvaluationDto> ToDtoAsync(Evaluation evaluation, CancellationToken cancellationToken)
    {
        var position = await positions.GetAsync(evaluation.PositionId, cancellationToken);
        var names = position?.Criteria.ToDictionary(c => c.Id, c => c.Name)
            ?? new Dictionary<Guid, string>();
        var scores = await evidence.ListForEvaluationAsync(evaluation.Id, names, cancellationToken);

        return new EvaluationDto(
            evaluation.Id,
            evaluation.PositionId,
            evaluation.CandidateId,
            evaluation.OverallScore,
            evaluation.CoverageRatio,
            evaluation.Status,
            evaluation.PromptVersion,
            evaluation.RubricVersion,
            evaluation.ModelName,
            evaluation.ModelVersion,
            evaluation.Summary,
            Split(evaluation.FollowUpsJson),
            Split(evaluation.NeedsVerificationJson),
            scores,
            evaluation.ExecutedAt,
            evaluation.FailureStage,
            evaluation.FailureMessage);
    }

    private static IReadOnlyList<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string BuildJobDescription(PositionSnapshot position)
    {
        var lines = new List<string>
        {
            position.Title,
            position.JobDescription,
            "Criteria:"
        };
        foreach (var criterion in position.Criteria)
        {
            lines.Add($"- {criterion.Name} ({criterion.Id:D}): {criterion.Description} weight={criterion.Weight}");
        }

        return string.Join('\n', lines);
    }

    private bool IsTesting =>
        string.Equals(env.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    private sealed record SummaryStub(string? Summary);
}
