using System.Text.Json.Serialization;

namespace HireLens.Contracts.Recruiting;

public sealed record PositionCriterionDto(Guid Id, string Name, string Description, int Weight);

public sealed record PositionDto(
    Guid Id,
    string Title,
    string JobDescription,
    IReadOnlyList<PositionCriterionDto> Criteria,
    DateTimeOffset CreatedAt,
    string? Slug = null,
    PositionStatsDto? Stats = null,
    [property: JsonPropertyName("interviewQuestions")]
    IReadOnlyList<ExtractedInterviewQuestionDto>? InterviewQuestions = null,
    IReadOnlyList<UnmeasurablePhraseDto>? Unmeasurable = null,
    IReadOnlyList<FlaggedPhraseDto>? FlaggedPhrases = null);

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
    IReadOnlyList<UpsertCriterionRequest> Criteria,
    [property: JsonPropertyName("interviewQuestions")]
    IReadOnlyList<ExtractedInterviewQuestionDto>? InterviewQuestions = null,
    IReadOnlyList<UnmeasurablePhraseDto>? Unmeasurable = null,
    IReadOnlyList<FlaggedPhraseDto>? FlaggedPhrases = null);

public sealed record UpsertCriterionRequest(string Name, string Description, int Weight);

public sealed record ExtractCriteriaRequest(string JobTitle, string JobDescription);

public sealed record ExtractedCriterionDto(string Label, string Description, int Weight, bool Mandatory);

public sealed record FlaggedPhraseDto(string Phrase, string Category, string Reason);

public sealed record UnmeasurablePhraseDto(string Phrase, string Reason);

public sealed record ExtractedInterviewQuestionDto(
    string QuestionId,
    string CriterionId,
    string Question,
    IReadOnlyList<string> WhatToListenFor)
{
    public static Guid ResolveCriterionId(
        IReadOnlyList<PositionCriterionDto> criteria,
        string? criterionIdOrName)
    {
        if (criteria.Count == 0)
        {
            return Guid.Empty;
        }

        if (!string.IsNullOrWhiteSpace(criterionIdOrName))
        {
            var needle = criterionIdOrName.Trim();
            var exact = criteria.FirstOrDefault(c =>
                string.Equals(c.Name, needle, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Id.ToString(), needle, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact.Id;
            }

            var fuzzy = criteria.FirstOrDefault(c =>
                c.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || needle.Contains(c.Name, StringComparison.OrdinalIgnoreCase));
            if (fuzzy is not null)
            {
                return fuzzy.Id;
            }
        }

        return criteria[0].Id;
    }
}

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
    IReadOnlyList<PositionCriterionDto> Criteria,
    IReadOnlyList<ExtractedInterviewQuestionDto>? InterviewQuestions = null);

public interface IPositionReadPort
{
    Task<PositionSnapshot?> GetAsync(Guid positionId, CancellationToken cancellationToken);
}

public interface IPositionWritePort
{
    Task<SharedKernel.Result<PositionDto>> CreateAsync(UpsertPositionRequest request, CancellationToken cancellationToken);
}
