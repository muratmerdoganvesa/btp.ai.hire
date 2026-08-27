using System.Text.Json;
using System.Text.Json.Serialization;
using HireLens.AiGateway.Providers;
using HireLens.Contracts.Evidence;
using HireLens.Contracts.Recruiting;

namespace HireLens.Modules.Matching.Application;

/// <summary>
/// Maps criteria-matching-v1 JSON into evidence-bound criterion scores.
/// Overall total is still computed in C# (ScoreCalculator).
/// </summary>
public static class CriteriaMatchingMapper
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
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(StripFence(content));
            var root = doc.RootElement;
            var note = root.TryGetProperty("note", out var n) ? n.GetString() : null;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            return string.Equals(note, "stub-provider", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(status, "unknown", StringComparison.OrdinalIgnoreCase)
                    && !root.TryGetProperty("criteria", out _));
        }
        catch (JsonException)
        {
            return true;
        }
    }

    public static IReadOnlyList<ProposedCriterionScore>? TryMap(string? content, PositionSnapshot position)
    {
        if (IsStubContent(content) || position.Criteria.Count == 0)
        {
            return null;
        }

        var payload = Deserialize(content);
        if (payload?.Criteria is null || payload.Criteria.Count == 0)
        {
            return null;
        }

        var proposals = new List<ProposedCriterionScore>();
        foreach (var criterion in position.Criteria)
        {
            var row = payload.Criteria.FirstOrDefault(c => Matches(c.CriterionId, criterion));
            if (row is null)
            {
                proposals.Add(new ProposedCriterionScore(criterion.Id, criterion.Weight, null, 0.2, []));
                continue;
            }

            var evidence = (row.Evidence ?? [])
                .Where(e => !string.IsNullOrWhiteSpace(e.Quote))
                .Select(e => new ProposedEvidence(
                    string.IsNullOrWhiteSpace(e.Source) ? "cv" : e.Source.Trim(),
                    e.Quote!.Trim(),
                    e.StartOffset ?? 0,
                    e.EndOffset ?? 0))
                .ToList();

            var score = row.Score;
            if (score is not null && evidence.Count == 0)
            {
                score = null;
            }

            proposals.Add(new ProposedCriterionScore(
                criterion.Id,
                criterion.Weight,
                score is null ? null : (int)Math.Clamp(Math.Round(score.Value), 0, 100),
                ToConfidence(row.Confidence, score),
                evidence));
        }

        return proposals;
    }

    private static bool Matches(string? criterionId, PositionCriterionDto criterion)
    {
        if (string.IsNullOrWhiteSpace(criterionId))
        {
            return false;
        }

        var needle = criterionId.Trim();
        return string.Equals(criterion.Id.ToString(), needle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(criterion.Name, needle, StringComparison.OrdinalIgnoreCase)
            || criterion.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || needle.Contains(criterion.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static double ToConfidence(string? label, double? score)
    {
        if (string.Equals(label, "high", StringComparison.OrdinalIgnoreCase))
        {
            return 0.9;
        }

        if (string.Equals(label, "medium", StringComparison.OrdinalIgnoreCase))
        {
            return 0.7;
        }

        if (string.Equals(label, "low", StringComparison.OrdinalIgnoreCase))
        {
            return 0.4;
        }

        if (string.Equals(label, "none", StringComparison.OrdinalIgnoreCase) || score is null)
        {
            return 0.2;
        }

        return 0.6;
    }

    private static MatchPayload? Deserialize(string? content)
    {
        var trimmed = StripFence(content!.Trim());
        trimmed = Unwrap(trimmed);
        try
        {
            return JsonSerializer.Deserialize<MatchPayload>(trimmed, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Unwrap(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("criteria", out _))
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

    private sealed class MatchPayload
    {
        public List<MatchCriterionAi>? Criteria { get; set; }

        public string? RecommendedAction { get; set; }
    }

    private sealed class MatchCriterionAi
    {
        public string? CriterionId { get; set; }

        public double? Score { get; set; }

        public string? Confidence { get; set; }

        public List<MatchEvidenceAi>? Evidence { get; set; }
    }

    private sealed class MatchEvidenceAi
    {
        public string? Quote { get; set; }

        public string? Source { get; set; }

        public int? StartOffset { get; set; }

        public int? EndOffset { get; set; }
    }
}
