using System.Text.Json;
using HireLens.Contracts.Recruiting;

namespace HireLens.Contracts.Interview;

/// <summary>
/// Input for the hosted interview-evaluation-v1 orchestration config.
/// Rubric and interview questions must come from the same jd-criteria-extraction run.
/// </summary>
public sealed record EvaluateInterviewRequest(
    JsonElement Rubric,
    IReadOnlyList<ExtractedInterviewQuestionDto> InterviewQuestions,
    string Transcript,
    JsonElement CvMatchResult = default,
    string? JobTitle = null);

public sealed record InterviewEvaluationResponse(
    string? RubricId,
    string? RubricVersion,
    int? OverallScore,
    IReadOnlyList<InterviewEvaluatedCriterionDto> Criteria,
    IReadOnlyList<InterviewConsistencyDto> Consistency,
    IReadOnlyList<InterviewEvaluationEvidenceDto> Evidence,
    IReadOnlyList<string> Warnings,
    string? Summary);

public sealed record InterviewEvaluatedCriterionDto(
    string CriterionId,
    string? QuestionId,
    int? Score,
    string? Confidence,
    string? Status,
    string? Reasoning,
    IReadOnlyList<InterviewEvaluationEvidenceDto> Evidence);

public sealed record InterviewConsistencyDto(
    string CriterionId,
    int? CvScore,
    int? InterviewScore,
    bool? Aligned,
    string? Detail);

public sealed record InterviewEvaluationEvidenceDto(
    string Quote,
    string Source,
    string? Speaker,
    string? Timestamp,
    string? QuestionId,
    string? CriterionId);
