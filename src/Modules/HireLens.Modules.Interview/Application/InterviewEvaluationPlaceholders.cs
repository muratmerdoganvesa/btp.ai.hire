using System.Text.Json;
using HireLens.Contracts.Interview;
using HireLens.Contracts.Recruiting;
using HireLens.SharedKernel;

namespace HireLens.Modules.Interview.Application;

/// <summary>
/// Builds the five string placeholder_values for interview-evaluation-v1.
/// Transcript stays plain text; everything else is JSON text or "".
/// </summary>
public static class InterviewEvaluationPlaceholders
{
    public const string JobTitle = "job_title";
    public const string Rubric = "rubric";
    public const string InterviewQuestions = "interview_questions";
    public const string CvMatchResult = "cv_match_result";
    public const string Transcript = "transcript";

    private static readonly JsonSerializerOptions Camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static Result<IReadOnlyDictionary<string, string>> TryBuild(EvaluateInterviewRequest request)
    {
        var rubricJson = ToJsonText(request.Rubric);
        if (string.IsNullOrWhiteSpace(rubricJson) || CountCriteria(request.Rubric) == 0)
        {
            return Result.Failure<IReadOnlyDictionary<string, string>>(
                Error.Validation("Rubrik kriteri yok; mülakat değerlendirmesi yapılmadı."));
        }

        var questions = (request.InterviewQuestions ?? [])
            .Where(q => !string.IsNullOrWhiteSpace(q.Question))
            .ToList();
        if (questions.Count == 0)
        {
            return Result.Failure<IReadOnlyDictionary<string, string>>(
                Error.Validation("Mülakat soruları yok; değerlendirme atlandı."));
        }

        var cvMatchJson = ToJsonText(request.CvMatchResult);
        var rubricId = ReadRubricId(request.Rubric);
        var matchRubricId = string.IsNullOrWhiteSpace(cvMatchJson)
            ? null
            : ReadRubricId(request.CvMatchResult);
        if (!string.IsNullOrWhiteSpace(rubricId)
            && !string.IsNullOrWhiteSpace(matchRubricId)
            && !string.Equals(rubricId, matchRubricId, StringComparison.Ordinal))
        {
            return Result.Failure<IReadOnlyDictionary<string, string>>(
                Error.Validation(
                    "rubricId uyuşmuyor: rubrik ile CV eşleştirmesi farklı çalıştırmalara ait."));
        }

        IReadOnlyDictionary<string, string> variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [JobTitle] = request.JobTitle?.Trim() ?? string.Empty,
            [Rubric] = rubricJson,
            [InterviewQuestions] = JsonSerializer.Serialize(
                questions.Select(q => new ExtractedInterviewQuestionDto(
                    q.QuestionId ?? string.Empty,
                    q.CriterionId ?? string.Empty,
                    q.Question.Trim(),
                    q.WhatToListenFor ?? [])),
                Camel),
            [CvMatchResult] = cvMatchJson,
            [Transcript] = request.Transcript ?? string.Empty
        };

        return Result.Success(variables);
    }

    internal static string ToJsonText(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return string.Empty;
        }

        if (element.ValueKind is JsonValueKind.String)
        {
            return element.GetString() ?? string.Empty;
        }

        return element.GetRawText();
    }

    internal static int CountCriteria(JsonElement rubric)
    {
        var obj = AsObject(rubric);
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        if (obj.TryGetProperty("criteria", out var criteria) && criteria.ValueKind == JsonValueKind.Array)
        {
            return criteria.GetArrayLength();
        }

        return 0;
    }

    internal static string? ReadRubricId(JsonElement element)
    {
        var obj = AsObject(element);
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (obj.TryGetProperty("rubricId", out var id) || obj.TryGetProperty("rubric_id", out id))
        {
            return string.IsNullOrWhiteSpace(id.GetString()) ? null : id.GetString();
        }

        return null;
    }

    private static JsonElement AsObject(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return element;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return default;
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return default;
            }
        }

        return default;
    }
}
