namespace HireLens.Contracts.Evidence;

public enum EvidenceStatus
{
    Sufficient = 1,
    Insufficient = 2,
    Unknown = 3
}

public sealed record EvidenceDto(
    string Source,
    string Quote,
    int StartOffset,
    int EndOffset);

public sealed record CriterionScoreDto(
    Guid CriterionId,
    string CriterionName,
    int? Score,
    int Weight,
    double Confidence,
    EvidenceStatus EvidenceStatus,
    IReadOnlyList<EvidenceDto> Evidence);

public sealed record ProposedEvidence(string Source, string Quote, int StartOffset, int EndOffset);

public sealed record ProposedCriterionScore(
    Guid CriterionId,
    int Weight,
    int? Score,
    double Confidence,
    IReadOnlyList<ProposedEvidence> Evidence);

public interface IEvidenceScoring
{
    Task ApplyAsync(
        Guid evaluationId,
        IReadOnlyList<ProposedCriterionScore> proposals,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CriterionScoreDto>> ListForEvaluationAsync(
        Guid evaluationId,
        IReadOnlyDictionary<Guid, string> criterionNames,
        CancellationToken cancellationToken);
}
