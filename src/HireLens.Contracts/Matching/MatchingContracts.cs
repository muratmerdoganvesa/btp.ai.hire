using HireLens.Contracts.Evidence;

namespace HireLens.Contracts.Matching;

public sealed record EvaluationDto(
    Guid Id,
    Guid PositionId,
    Guid CandidateId,
    int? OverallScore,
    decimal CoverageRatio,
    string Status,
    string PromptVersion,
    string RubricVersion,
    string ModelName,
    string ModelVersion,
    string? Summary,
    IReadOnlyList<string> FollowUps,
    IReadOnlyList<string> NeedsVerification,
    IReadOnlyList<CriterionScoreDto> Scores,
    DateTimeOffset? ExecutedAt = null,
    string? FailureStage = null,
    string? FailureMessage = null);

public sealed record EvaluationAuditDto(
    Guid EvaluationId,
    string PromptVersion,
    string RubricVersion,
    string ModelName,
    string ModelVersion,
    decimal CoverageRatio,
    DateTimeOffset? ExecutedAt,
    string Status);

public interface IEvaluationReadPort
{
    Task<EvaluationDto?> GetForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    Task<EvaluationDto?> GetByIdAsync(Guid evaluationId, CancellationToken cancellationToken);

    Task<EvaluationAuditDto?> GetAuditAsync(Guid evaluationId, CancellationToken cancellationToken);
}

public interface IEvaluationWritePort
{
    /// <summary>Starts an async evaluation; returns evaluation id for polling.</summary>
    Task<Guid> StartAsync(Guid candidateId, Guid jobDescriptionId, CancellationToken cancellationToken);
}

public interface IEvaluationBlendPort
{
    Task BlendInterviewAsync(Guid candidateId, int? interviewScore, int interviewWeight, CancellationToken cancellationToken);
}

public interface IAnalysisJobs
{
    string EnqueueDocumentParse(Guid tenantId, Guid documentId);

    string EnqueueMatching(Guid tenantId, Guid documentId);

    string EnqueueEvaluation(Guid tenantId, Guid evaluationId);
}
