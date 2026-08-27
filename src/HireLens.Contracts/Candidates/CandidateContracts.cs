namespace HireLens.Contracts.Candidates;

public sealed record CandidateDto(
    Guid Id,
    Guid PositionId,
    string DisplayName,
    string? OverallScoreLabel,
    int? OverallScore,
    string Status,
    DateTimeOffset CreatedAt,
    decimal? CoverageRatio = null,
    string? RecommendedAction = null,
    string? EvaluationStatus = null,
    int RiskFlagCount = 0);

/// <summary>Tenant-wide candidate board row (one application = one position).</summary>
public sealed record CandidateBoardItemDto(
    Guid Id,
    Guid PositionId,
    string PositionTitle,
    string DisplayName,
    string PersonKey,
    int SiblingApplicationCount,
    string? OverallScoreLabel,
    int? OverallScore,
    string Status,
    string PipelineStage,
    string? RecommendedAction,
    DateTimeOffset CreatedAt);

public sealed record CreateCandidateRequest(string DisplayName);

public sealed record CandidateSnapshot(Guid Id, Guid PositionId, string DisplayName);

public sealed record CandidateEvaluationSummary(
    int? OverallScore,
    decimal? CoverageRatio,
    string? EvaluationStatus,
    int RiskFlagCount,
    string? RecommendedAction);

public interface ICandidateEvaluationSummaryPort
{
    Task<IReadOnlyDictionary<Guid, CandidateEvaluationSummary>> GetForCandidatesAsync(
        IReadOnlyList<Guid> candidateIds,
        CancellationToken cancellationToken);
}

public interface ICandidateReadPort
{
    Task<CandidateSnapshot?> GetAsync(Guid candidateId, CancellationToken cancellationToken);
}

public interface ICandidateWritePort
{
    Task<SharedKernel.Result<CandidateDto>> CreateAsync(Guid positionId, CreateCandidateRequest request, CancellationToken cancellationToken);
}
