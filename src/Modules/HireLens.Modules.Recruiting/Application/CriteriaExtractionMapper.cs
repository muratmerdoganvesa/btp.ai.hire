using System.Text.Json;
using System.Text.Json.Serialization;
using HireLens.AiGateway.Providers;
using HireLens.Contracts.Recruiting;

namespace HireLens.Modules.Recruiting.Application;

/// <summary>
/// Maps jd-criteria-extraction JSON (rubric.criteria + interviewQuestions) to the API DTO.
/// </summary>
public static class CriteriaExtractionMapper
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
                    && !root.TryGetProperty("rubric", out _)
                    && !root.TryGetProperty("criteria", out _));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static ExtractCriteriaResponse Parse(string? content)
    {
        var payload = Deserialize(content) ?? new CriteriaExtractionAiPayload();
        return Normalize(payload);
    }

    internal static IReadOnlyList<ExtractedCriterionDto> NormalizeWeights(
        IReadOnlyList<ExtractedCriterionDto> criteria)
    {
        if (criteria.Count == 0)
        {
            return criteria;
        }

        var sum = criteria.Sum(c => c.Weight);
        if (sum == 100)
        {
            return criteria;
        }

        if (sum <= 0)
        {
            var baseWeight = 100 / criteria.Count;
            var remainder = 100 - (baseWeight * criteria.Count);
            return criteria
                .Select((c, i) => c with { Weight = baseWeight + (i < remainder ? 1 : 0) })
                .ToList();
        }

        var scaled = criteria
            .Select(c => c with { Weight = (int)Math.Round(c.Weight * 100.0 / sum, MidpointRounding.AwayFromZero) })
            .ToList();

        var scaledSum = scaled.Sum(c => c.Weight);
        var diff = 100 - scaledSum;
        if (diff != 0)
        {
            var index = scaled
                .Select((c, i) => (c.Weight, i))
                .OrderByDescending(x => x.Weight)
                .ThenBy(x => x.i)
                .First()
                .i;
            scaled[index] = scaled[index] with { Weight = Math.Max(1, scaled[index].Weight + diff) };
        }

        return scaled;
    }

    private static CriteriaExtractionAiPayload? Deserialize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var trimmed = StripFence(content.Trim());
        trimmed = UnwrapOrchestrationEnvelope(trimmed);

        try
        {
            return JsonSerializer.Deserialize<CriteriaExtractionAiPayload>(trimmed, JsonOptions);
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
            if (root.TryGetProperty("rubric", out _) || root.TryGetProperty("criteria", out _))
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

    private static ExtractCriteriaResponse Normalize(CriteriaExtractionAiPayload payload)
    {
        var rawCriteria = ExtractCriteria(payload);
        var criteria = NormalizeWeights(rawCriteria);

        var flagged = (payload.FlaggedPhrases ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Phrase))
            .Select(f => new FlaggedPhraseDto(
                f.Phrase!.Trim(),
                f.Category?.Trim() ?? string.Empty,
                f.Reason?.Trim() ?? string.Empty))
            .ToList();

        var unmeasurable = (payload.Unmeasurable ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u.Phrase))
            .Select(u => new UnmeasurablePhraseDto(
                u.Phrase!.Trim(),
                u.Reason?.Trim() ?? string.Empty))
            .ToList();

        var interviewQuestions = (payload.InterviewQuestions ?? [])
            .Where(q => !string.IsNullOrWhiteSpace(q.Question))
            .Select(q => new ExtractedInterviewQuestionDto(
                string.IsNullOrWhiteSpace(q.QuestionId) ? string.Empty : q.QuestionId.Trim(),
                string.IsNullOrWhiteSpace(q.CriterionId) ? string.Empty : q.CriterionId.Trim(),
                q.Question!.Trim(),
                (q.WhatToListenFor ?? [])
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Select(h => h.Trim())
                    .ToList()))
            .Take(5)
            .ToList();

        var warnings = (payload.Warnings ?? [])
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new ExtractCriteriaResponse(
            criteria,
            flagged,
            unmeasurable,
            criteria.Sum(c => c.Weight),
            interviewQuestions,
            warnings);
    }

    private static List<ExtractedCriterionDto> ExtractCriteria(CriteriaExtractionAiPayload payload)
    {
        var fromRubric = (payload.Rubric?.Criteria ?? [])
            .Select(MapCriterion)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        if (fromRubric.Count > 0)
        {
            return fromRubric;
        }

        return (payload.Criteria ?? [])
            .Select(MapCriterion)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();
    }

    private static ExtractedCriterionDto? MapCriterion(CriterionAi c)
    {
        var label = FirstNonEmpty(c.Name, c.Label, c.Title);
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var description = string.IsNullOrWhiteSpace(c.Description) ? label : c.Description.Trim();
        return new ExtractedCriterionDto(
            label.Trim(),
            description,
            Math.Max(0, c.Weight),
            c.Mandatory);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private sealed class CriteriaExtractionAiPayload
    {
        public RubricAi? Rubric { get; set; }

        public List<CriterionAi>? Criteria { get; set; }

        public List<InterviewQuestionAi>? InterviewQuestions { get; set; }

        public List<string>? Warnings { get; set; }

        public List<FlaggedAi>? FlaggedPhrases { get; set; }

        public List<UnmeasurableAi>? Unmeasurable { get; set; }

        public int? TotalWeight { get; set; }
    }

    private sealed class RubricAi
    {
        public List<CriterionAi>? Criteria { get; set; }

        public int? WeightTotal { get; set; }
    }

    private sealed class CriterionAi
    {
        public string? CriterionId { get; set; }

        public string? Name { get; set; }

        public string? Label { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public int Weight { get; set; }

        public bool Mandatory { get; set; }
    }

    private sealed class InterviewQuestionAi
    {
        public string? QuestionId { get; set; }

        public string? CriterionId { get; set; }

        public string? Question { get; set; }

        public List<string>? WhatToListenFor { get; set; }
    }

    private sealed class FlaggedAi
    {
        public string? Phrase { get; set; }

        public string? Category { get; set; }

        public string? Reason { get; set; }
    }

    private sealed class UnmeasurableAi
    {
        public string? Phrase { get; set; }

        public string? Reason { get; set; }
    }
}
