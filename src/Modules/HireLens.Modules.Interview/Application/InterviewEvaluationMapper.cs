using System.Text.Json;
using System.Text.Json.Serialization;
using HireLens.AiGateway.Providers;
using HireLens.Contracts.Interview;

namespace HireLens.Modules.Interview.Application;

/// <summary>
/// Maps interview-evaluation-v1 JSON to the API DTO.
/// </summary>
public static class InterviewEvaluationMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new FlexibleIntConverter() }
    };

    public static bool IsStubContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(StripFence(content));
            var root = doc.RootElement;
            var note = root.TryGetProperty("note", out var n) ? n.GetString() : null;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            return string.Equals(note, "stub-provider", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(status, "unknown", StringComparison.OrdinalIgnoreCase)
                    && !root.TryGetProperty("criteria", out _)
                    && !root.TryGetProperty("criterionScores", out _)
                    && !root.TryGetProperty("warnings", out _));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static InterviewEvaluationResponse Parse(string? content)
    {
        var payload = Deserialize(content) ?? new EvaluationAiPayload();
        return Normalize(payload);
    }

    private static EvaluationAiPayload? Deserialize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var trimmed = StripFence(content.Trim());
        trimmed = UnwrapOrchestrationEnvelope(trimmed);

        try
        {
            return JsonSerializer.Deserialize<EvaluationAiPayload>(trimmed, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string UnwrapOrchestrationEnvelope(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("criteria", out _)
                || root.TryGetProperty("criterionScores", out _)
                || root.TryGetProperty("warnings", out _)
                || root.TryGetProperty("consistency", out _)
                || root.TryGetProperty("answers", out _))
            {
                return json;
            }

            if (root.TryGetProperty("orchestration_result", out _)
                || root.TryGetProperty("module_results", out _))
            {
                var extracted = OrchestrationContentExtractor.Extract(json).Content;
                if (!string.IsNullOrWhiteSpace(extracted) && extracted != json)
                {
                    return StripFence(extracted.Trim());
                }
            }
        }
        catch (JsonException)
        {
            /* keep original */
        }

        return json;
    }

    private static string StripFence(string trimmed)
    {
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline > 0)
        {
            trimmed = trimmed[(firstNewline + 1)..];
        }

        var fence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            trimmed = trimmed[..fence];
        }

        return trimmed.Trim();
    }

    private static InterviewEvaluationResponse Normalize(EvaluationAiPayload payload)
    {
        var criteria = (payload.CriterionScores is { Count: > 0 } scores
                ? scores.Select(MapCriterionScore)
                : (payload.Criteria ?? []).Select(MapCriterion))
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();
        criteria = MergeAnswers(criteria, payload.Answers ?? []);

        var consistency = (payload.Consistency ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.CriterionId))
            .Select(c => new InterviewConsistencyDto(
                c.CriterionId!.Trim(),
                c.CvScore,
                c.InterviewScore,
                c.Aligned,
                ConsistencyDetail(c)))
            .ToList();

        var evidence = (payload.Evidence ?? [])
            .Select(MapEvidence)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
        if (evidence.Count == 0)
        {
            evidence = criteria.SelectMany(c => c.Evidence).ToList();
        }

        var warnings = NormalizeWarnings(payload.Warnings);

        return new InterviewEvaluationResponse(
            string.IsNullOrWhiteSpace(payload.RubricId) ? null : payload.RubricId.Trim(),
            string.IsNullOrWhiteSpace(payload.RubricVersion) ? null : payload.RubricVersion.Trim(),
            payload.OverallScore,
            criteria,
            consistency,
            evidence,
            warnings,
            string.IsNullOrWhiteSpace(payload.Summary) ? null : payload.Summary.Trim());
    }

    private static List<InterviewEvaluatedCriterionDto> MergeAnswers(
        List<InterviewEvaluatedCriterionDto> criteria,
        List<AnswerAi> answers)
    {
        var usable = answers.Where(a => !string.IsNullOrWhiteSpace(a.CriterionId)).ToList();
        if (usable.Count == 0)
        {
            return criteria;
        }

        if (criteria.Count == 0)
        {
            return usable
                .GroupBy(a => a.CriterionId!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => MapAnswerGroup(g.Key, g.ToList()))
                .Where(c => c is not null)
                .Select(c => c!)
                .ToList();
        }

        var grouped = usable
            .GroupBy(a => a.CriterionId!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return criteria
            .Select(row => grouped.TryGetValue(row.CriterionId, out var group)
                ? EnrichFromAnswers(row, group)
                : row)
            .ToList();
    }

    private static InterviewEvaluatedCriterionDto? MapAnswerGroup(string criterionId, List<AnswerAi> answers)
    {
        if (string.IsNullOrWhiteSpace(criterionId) || answers.Count == 0)
        {
            return null;
        }

        return EnrichFromAnswers(
            new InterviewEvaluatedCriterionDto(criterionId, null, null, null, null, null, []),
            answers);
    }

    private static InterviewEvaluatedCriterionDto EnrichFromAnswers(
        InterviewEvaluatedCriterionDto existing,
        List<AnswerAi> answers)
    {
        var answerEvidence = answers
            .SelectMany(a => a.Evidence ?? [])
            .Select(MapEvidence)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
        var evidence = existing.Evidence.Count > 0
            ? existing.Evidence.Concat(answerEvidence).DistinctBy(e => e.Quote).ToList()
            : answerEvidence;

        var missing = answers
            .SelectMany(a => a.MissingEvidence ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var followUps = answers
            .Select(a => a.FollowUpQuestion)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var statuses = answers
            .Select(a => a.AnswerStatus)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var reasoning = JoinNonEmpty(
            existing.Reasoning,
            statuses.Count == 0 ? null : "answerStatus: " + string.Join(", ", statuses),
            missing.Count == 0 ? null : "Eksik kanıt: " + string.Join("; ", missing),
            followUps.Count == 0 ? null : "Doğrulama: " + string.Join(" ", followUps));

        return existing with
        {
            QuestionId = existing.QuestionId
                ?? answers.Select(a => a.QuestionId).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim(),
            Score = existing.Score ?? answers.Select(a => a.Score).FirstOrDefault(s => s is not null),
            Confidence = existing.Confidence
                ?? answers.Select(a => a.Confidence).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim(),
            Status = IsVerificationSource(existing.Status)
                ? existing.Status
                : statuses.Count == 0 ? existing.Status : statuses[0],
            Reasoning = reasoning,
            Evidence = evidence
        };
    }

    private static bool IsVerificationSource(string? status) =>
        status is not null
        && (status.Equals("interview", StringComparison.OrdinalIgnoreCase)
            || status.Equals("cv", StringComparison.OrdinalIgnoreCase)
            || status.Equals("both", StringComparison.OrdinalIgnoreCase)
            || status.Equals("document_required", StringComparison.OrdinalIgnoreCase)
            || status.Equals("none", StringComparison.OrdinalIgnoreCase));

    private static string? JoinNonEmpty(params string?[] parts)
    {
        var values = parts
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .ToList();
        return values.Count == 0 ? null : string.Join(" ", values);
    }

    private static InterviewEvaluatedCriterionDto? MapCriterion(CriterionAi c)
    {
        if (string.IsNullOrWhiteSpace(c.CriterionId))
        {
            return null;
        }

        var evidence = (c.Evidence ?? [])
            .Select(MapEvidence)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        return new InterviewEvaluatedCriterionDto(
            c.CriterionId.Trim(),
            string.IsNullOrWhiteSpace(c.QuestionId) ? null : c.QuestionId.Trim(),
            c.Score,
            string.IsNullOrWhiteSpace(c.Confidence) ? null : c.Confidence.Trim(),
            string.IsNullOrWhiteSpace(c.Status) ? null : c.Status.Trim(),
            string.IsNullOrWhiteSpace(c.Reasoning) ? null : c.Reasoning.Trim(),
            evidence);
    }

    private static InterviewEvaluatedCriterionDto? MapCriterionScore(CriterionScoreAi c) =>
        MapCriterion(new CriterionAi
        {
            CriterionId = c.CriterionId,
            Score = c.InterviewScore,
            Confidence = c.Confidence,
            Status = c.VerificationSource,
            Reasoning = c.Rationale,
            Evidence = c.Evidence
        });

    private static string? ConsistencyDetail(ConsistencyAi c)
    {
        if (!string.IsNullOrWhiteSpace(c.Detail))
        {
            return c.Detail.Trim();
        }

        var parts = new[] { c.CvClaim, c.InterviewClaim, c.Severity }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .ToList();
        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static IReadOnlyList<string> NormalizeWarnings(JsonElement warnings)
    {
        if (warnings.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null or not JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<string>();
        foreach (var item in warnings.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value.Trim());
                }
            }
            else if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("code", out var code))
            {
                var value = code.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value.Trim());
                }
            }
        }

        return list.Distinct(StringComparer.Ordinal).ToList();
    }

    private static InterviewEvaluationEvidenceDto? MapEvidence(EvidenceAi? e)
    {
        if (e is null || string.IsNullOrWhiteSpace(e.Quote))
        {
            return null;
        }

        return new InterviewEvaluationEvidenceDto(
            e.Quote.Trim(),
            string.IsNullOrWhiteSpace(e.Source) ? "interview" : e.Source.Trim(),
            string.IsNullOrWhiteSpace(e.Speaker) ? null : e.Speaker.Trim(),
            string.IsNullOrWhiteSpace(e.Timestamp) ? null : e.Timestamp.Trim(),
            string.IsNullOrWhiteSpace(e.QuestionId) ? null : e.QuestionId.Trim(),
            string.IsNullOrWhiteSpace(e.CriterionId) ? null : e.CriterionId.Trim());
    }

    private sealed class EvaluationAiPayload
    {
        public string? RubricId { get; set; }

        public string? RubricVersion { get; set; }

        public int? OverallScore { get; set; }

        public List<CriterionAi>? Criteria { get; set; }

        public List<CriterionScoreAi>? CriterionScores { get; set; }

        public List<AnswerAi>? Answers { get; set; }

        public List<ConsistencyAi>? Consistency { get; set; }

        public List<EvidenceAi>? Evidence { get; set; }

        public JsonElement Warnings { get; set; }

        public string? Summary { get; set; }
    }

    private sealed class CriterionAi
    {
        public string? CriterionId { get; set; }

        public string? QuestionId { get; set; }

        public int? Score { get; set; }

        public string? Confidence { get; set; }

        public string? Status { get; set; }

        public string? Reasoning { get; set; }

        public List<EvidenceAi>? Evidence { get; set; }
    }

    private sealed class CriterionScoreAi
    {
        public string? CriterionId { get; set; }

        public int? InterviewScore { get; set; }

        public int? CvScore { get; set; }

        public string? VerificationSource { get; set; }

        public string? Rationale { get; set; }

        public string? Confidence { get; set; }

        public List<EvidenceAi>? Evidence { get; set; }
    }

    private sealed class ConsistencyAi
    {
        public string? CriterionId { get; set; }

        public int? CvScore { get; set; }

        public int? InterviewScore { get; set; }

        public bool? Aligned { get; set; }

        public string? Detail { get; set; }

        public string? CvClaim { get; set; }

        public string? InterviewClaim { get; set; }

        public string? Severity { get; set; }
    }

    private sealed class EvidenceAi
    {
        public string? Quote { get; set; }

        public string? Source { get; set; }

        public string? Speaker { get; set; }

        public string? Timestamp { get; set; }

        public string? QuestionId { get; set; }

        public string? CriterionId { get; set; }
    }

    private sealed class AnswerAi
    {
        public string? QuestionId { get; set; }

        public string? CriterionId { get; set; }

        public string? AnswerStatus { get; set; }

        public int? Score { get; set; }

        public List<EvidenceAi>? Evidence { get; set; }

        public List<string>? MissingEvidence { get; set; }

        public string? FollowUpQuestion { get; set; }

        public string? Confidence { get; set; }
    }

    private sealed class FlexibleIntConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.Number when reader.TryGetInt32(out var i) => i,
                JsonTokenType.Number => (int)Math.Round(reader.GetDouble()),
                JsonTokenType.String when int.TryParse(reader.GetString(), out var parsed) => parsed,
                JsonTokenType.String => null,
                _ => null
            };
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteNumberValue(value.Value);
        }
    }
}
