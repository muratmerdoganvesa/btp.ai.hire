using HireLens.Contracts.Evidence;

namespace HireLens.Contracts.Matching;

public sealed record EvaluationDto(
    Guid Id,
    Guid PositionId,
    Guid CandidateId,
    int? OverallScore,
    string Status,
    string PromptVersion,
    string? Summary,
    IReadOnlyList<string> FollowUps,
    IReadOnlyList<string> NeedsVerification,
    IReadOnlyList<CriterionScoreDto> Scores);

public interface IEvaluationReadPort
{
    Task<EvaluationDto?> GetForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);
}

public interface IEvaluationBlendPort
{
    Task BlendInterviewAsync(Guid candidateId, int? interviewScore, int interviewWeight, CancellationToken cancellationToken);
}

public interface IAnalysisJobs
{
    string EnqueueDocumentParse(Guid tenantId, Guid documentId);

    string EnqueueMatching(Guid tenantId, Guid documentId);
}
