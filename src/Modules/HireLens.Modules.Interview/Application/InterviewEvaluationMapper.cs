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
        NumberHandling = JsonNumberHandling.AllowReadingFromString
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
                || root.TryGetProperty("warnings", out _)
                || root.TryGetProperty("consistency", out _))
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
        var criteria = (payload.Criteria ?? [])
            .Select(MapCriterion)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        var consistency = (payload.Consistency ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.CriterionId))
            .Select(c => new InterviewConsistencyDto(
                c.CriterionId!.Trim(),
                c.CvScore,
                c.InterviewScore,
                c.Aligned,
                string.IsNullOrWhiteSpace(c.Detail) ? null : c.Detail.Trim()))
            .ToList();

        var evidence = (payload.Evidence ?? [])
            .Select(MapEvidence)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        var warnings = (payload.Warnings ?? [])
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

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

        public List<ConsistencyAi>? Consistency { get; set; }

        public List<EvidenceAi>? Evidence { get; set; }

        public List<string>? Warnings { get; set; }

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

    private sealed class ConsistencyAi
    {
        public string? CriterionId { get; set; }

        public int? CvScore { get; set; }

        public int? InterviewScore { get; set; }

        public bool? Aligned { get; set; }

        public string? Detail { get; set; }
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
}
