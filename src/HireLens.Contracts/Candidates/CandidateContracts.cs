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

public sealed record CreateCandidateRequest(string DisplayName);

public sealed record CandidateSnapshot(Guid Id, Guid PositionId, string DisplayName);

public interface ICandidateReadPort
{
    Task<CandidateSnapshot?> GetAsync(Guid candidateId, CancellationToken cancellationToken);
}

public interface ICandidateWritePort
{
    Task<SharedKernel.Result<CandidateDto>> CreateAsync(Guid positionId, CreateCandidateRequest request, CancellationToken cancellationToken);
}
