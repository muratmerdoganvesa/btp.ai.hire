namespace HireLens.Contracts.Recruiting;

public sealed record PositionCriterionDto(Guid Id, string Name, string Description, int Weight);

public sealed record PositionDto(
    Guid Id,
    string Title,
    string JobDescription,
    IReadOnlyList<PositionCriterionDto> Criteria,
    DateTimeOffset CreatedAt,
    string? Slug = null,
    PositionStatsDto? Stats = null);

public interface IPositionStatsPort
{
    Task<IReadOnlyDictionary<Guid, PositionStatsDto>> GetForPositionsAsync(
        IReadOnlyList<Guid> positionIds,
        CancellationToken cancellationToken);
}

public sealed record PositionStatsDto(
    int TotalCandidates,
    int EvaluatedCount,
    int PendingCount,
    int FailedCount,
    int ReviewPendingCount);

public sealed record PublicJobDto(
    Guid Id,
    string Slug,
    string Title,
    string JobDescription,
    IReadOnlyList<PositionCriterionDto> Criteria,
    bool IsOpen);

public sealed record PublicApplicationRequest(
    string Slug,
    string DisplayName,
    string Email,
    string? Phone,
    string ConsentVersion,
    bool ConsentAccepted);

public sealed record PublicApplicationResponse(
    Guid ApplicationId,
    string ReferenceNumber,
    Guid DocumentId,
    string UploadUrl,
    string UploadMethod);

public sealed record PublicApplicationStatusDto(
    string ReferenceNumber,
    Guid ApplicationId,
    string Stage,
    bool RequiresReupload);

public sealed record UpsertPositionRequest(
    string Title,
    string JobDescription,
    IReadOnlyList<UpsertCriterionRequest> Criteria);

public sealed record UpsertCriterionRequest(string Name, string Description, int Weight);

public sealed record ExtractCriteriaRequest(string JobTitle, string JobDescription);

public sealed record ExtractedCriterionDto(string Label, string Description, int Weight, bool Mandatory);

public sealed record FlaggedPhraseDto(string Phrase, string Category, string Reason);

public sealed record UnmeasurablePhraseDto(string Phrase, string Reason);

public sealed record ExtractedInterviewQuestionDto(
    string QuestionId,
    string CriterionId,
    string Question,
    IReadOnlyList<string> WhatToListenFor);

public sealed record ExtractCriteriaResponse(
    IReadOnlyList<ExtractedCriterionDto> Criteria,
    IReadOnlyList<FlaggedPhraseDto> FlaggedPhrases,
    IReadOnlyList<UnmeasurablePhraseDto> Unmeasurable,
    int TotalWeight,
    IReadOnlyList<ExtractedInterviewQuestionDto> InterviewQuestions,
    IReadOnlyList<string> Warnings);

public sealed record PositionSnapshot(
    Guid Id,
    string Title,
    string JobDescription,
    IReadOnlyList<PositionCriterionDto> Criteria);

public interface IPositionReadPort
{
    Task<PositionSnapshot?> GetAsync(Guid positionId, CancellationToken cancellationToken);
}

public interface IPositionWritePort
{
    Task<SharedKernel.Result<PositionDto>> CreateAsync(UpsertPositionRequest request, CancellationToken cancellationToken);
}
