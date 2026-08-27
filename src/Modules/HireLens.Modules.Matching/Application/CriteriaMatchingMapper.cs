using System.Text.Json;
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
    public static bool IsStubContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(Normalize(content));
            var root = UnwrapRoot(doc.RootElement);
            var note = ReadString(root, "note");
            var status = ReadString(root, "status");
            return string.Equals(note, "stub-provider", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(status, "unknown", StringComparison.OrdinalIgnoreCase)
                    && FindCriteriaArray(root) is null);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static IReadOnlyList<ProposedCriterionScore>? TryMap(string? content, PositionSnapshot position)
    {
        if (position.Criteria.Count == 0 || IsStubContent(content))
        {
            return null;
        }

        var rows = ReadCriteria(content);
        if (rows.Count == 0)
        {
            return null;
        }

        var proposals = new List<ProposedCriterionScore>();
        foreach (var criterion in position.Criteria)
        {
            var row = rows.FirstOrDefault(c => Matches(c, criterion));
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

    private static bool Matches(MatchCriterionAi row, PositionCriterionDto criterion)
    {
        return MatchesId(row.CriterionId, criterion)
            || MatchesId(row.Name, criterion);
    }

    private static bool MatchesId(string? value, PositionCriterionDto criterion)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var needle = value.Trim();
        if (Guid.TryParse(needle, out var guid) && guid == criterion.Id)
        {
            return true;
        }

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

    private static List<MatchCriterionAi> ReadCriteria(string? content)
    {
        var prepared = StripFence((content ?? string.Empty).Trim());
        foreach (var candidate in JsonCandidates(prepared))
        {
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                var array = FindCriteriaArray(UnwrapRoot(doc.RootElement));
                if (array is not null)
                {
                    return ReadCriteriaArray(array.Value);
                }
            }
            catch (JsonException)
            {
                /* try next candidate / sliced array */
            }
        }

        if (TrySliceCriteriaArray(prepared, out var sliced))
        {
            try
            {
                using var doc = JsonDocument.Parse(sliced);
                return ReadCriteriaArray(doc.RootElement);
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return [];
    }

    private static List<MatchCriterionAi> ReadCriteriaArray(JsonElement array)
    {
        var rows = new List<MatchCriterionAi>();
        if (array.ValueKind != JsonValueKind.Array)
        {
            return rows;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            rows.Add(new MatchCriterionAi
            {
                CriterionId = ReadString(item, "criterionId", "criterion_id", "id"),
                Name = ReadString(item, "name", "label", "title"),
                Score = ReadNumber(item, "score", "points"),
                Confidence = ReadString(item, "confidence"),
                Evidence = ReadEvidence(item)
            });
        }

        return rows;
    }

    private static List<MatchEvidenceAi> ReadEvidence(JsonElement row)
    {
        if (!TryGetProperty(row, out var evidence, "evidence", "quotes"))
        {
            return [];
        }

        var items = new List<MatchEvidenceAi>();
        if (evidence.ValueKind == JsonValueKind.String)
        {
            var quote = evidence.GetString();
            if (!string.IsNullOrWhiteSpace(quote))
            {
                items.Add(new MatchEvidenceAi { Quote = quote, Source = "cv" });
            }

            return items;
        }

        if (evidence.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var part in evidence.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                var quote = part.GetString();
                if (!string.IsNullOrWhiteSpace(quote))
                {
                    items.Add(new MatchEvidenceAi { Quote = quote, Source = "cv" });
                }

                continue;
            }

            if (part.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var text = ReadString(part, "quote", "text", "excerpt", "span");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            items.Add(new MatchEvidenceAi
            {
                Quote = text,
                Source = ReadString(part, "source") ?? "cv",
                StartOffset = ReadInt(part, "startOffset", "start_offset"),
                EndOffset = ReadInt(part, "endOffset", "end_offset")
            });
        }

        return items;
    }

    private static JsonElement UnwrapRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return root;
        }

        if (root.TryGetProperty("orchestration_result", out _)
            || root.TryGetProperty("module_results", out _))
        {
            var extracted = OrchestrationContentExtractor.Extract(root.GetRawText()).Content;
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                try
                {
                    using var inner = JsonDocument.Parse(Normalize(extracted));
                    return inner.RootElement.Clone();
                }
                catch (JsonException)
                {
                    return root;
                }
            }
        }

        return root;
    }

    private static JsonElement? FindCriteriaArray(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetProperty(root, out var direct, "criteria", "criterionScores", "criterion_scores", "scores")
            && direct.ValueKind == JsonValueKind.Array
            && direct.GetArrayLength() > 0)
        {
            return direct;
        }

        foreach (var name in new[] { "result", "data", "match", "matching", "payload" })
        {
            if (root.TryGetProperty(name, out var nested)
                && nested.ValueKind == JsonValueKind.Object)
            {
                var found = FindCriteriaArray(nested);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> JsonCandidates(string prepared)
    {
        yield return prepared;
        var start = prepared.IndexOf('{');
        var end = prepared.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            var sliced = prepared[start..(end + 1)];
            if (sliced != prepared)
            {
                yield return sliced;
            }
        }
    }

    private static string Normalize(string content) => StripFence(content.Trim());

    private static bool TrySliceCriteriaArray(string json, out string arrayJson)
    {
        arrayJson = string.Empty;
        var key = json.IndexOf("\"criteria\"", StringComparison.OrdinalIgnoreCase);
        if (key < 0)
        {
            key = json.IndexOf("\"criterion_scores\"", StringComparison.OrdinalIgnoreCase);
        }

        if (key < 0)
        {
            return false;
        }

        var bracket = json.IndexOf('[', key);
        if (bracket < 0)
        {
            return false;
        }

        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = bracket; i < json.Length; i++)
        {
            var c = json[i];
            if (inString)
            {
                if (escape)
                {
                    escape = false;
                }
                else if (c == '\\')
                {
                    escape = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '[')
            {
                depth++;
            }
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                {
                    arrayJson = json[bracket..(i + 1)];
                    return true;
                }
            }
        }

        return false;
    }

    private static string StripFence(string trimmed)
    {
        var fenceStart = trimmed.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart < 0)
        {
            return trimmed;
        }

        var after = trimmed[(fenceStart + 3)..];
        var newline = after.IndexOf('\n');
        if (newline >= 0)
        {
            after = after[(newline + 1)..];
        }

        var fenceEnd = after.LastIndexOf("```", StringComparison.Ordinal);
        if (fenceEnd >= 0)
        {
            after = after[..fenceEnd];
        }

        return after.Trim();
    }

    private static bool TryGetProperty(JsonElement obj, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out value))
            {
                return true;
            }

            foreach (var property in obj.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement obj, params string[] names)
    {
        if (!TryGetProperty(obj, out var value, names) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static double? ReadNumber(JsonElement obj, params string[] names)
    {
        if (!TryGetProperty(obj, out var value, names) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? ReadInt(JsonElement obj, params string[] names)
    {
        var number = ReadNumber(obj, names);
        return number is null ? null : (int)Math.Round(number.Value);
    }

    private sealed class MatchCriterionAi
    {
        public string? CriterionId { get; set; }

        public string? Name { get; set; }

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
