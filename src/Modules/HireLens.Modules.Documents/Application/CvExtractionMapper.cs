using System.Text.Json;
using HireLens.AiGateway.Providers;

namespace HireLens.Modules.Documents.Application;

public static class CvExtractionMapper
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
            var root = doc.RootElement;
            var note = ReadString(root, "note");
            var status = ReadString(root, "status");
            return string.Equals(note, "stub-provider", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(status, "unknown", StringComparison.OrdinalIgnoreCase)
                    && !HasProfileSignal(root));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool IsUsable(string? content)
    {
        if (IsStubContent(content) || string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var root = TryReadProfile(content);
        if (root is null)
        {
            return false;
        }

        var quality = ReadString(root.Value, "parseQuality", "parse_quality");
        if (string.Equals(quality, "unusable", StringComparison.OrdinalIgnoreCase)
            && !HasExtractedFields(root.Value))
        {
            return false;
        }

        if (quality is "good" or "partial" or "poor")
        {
            return true;
        }

        return HasExtractedFields(root.Value);
    }

    private static JsonElement? TryReadProfile(string content)
    {
        foreach (var candidate in JsonCandidates(Normalize(content)))
        {
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                var root = Unwrap(doc.RootElement);
                if (root.ValueKind == JsonValueKind.Object)
                {
                    return root.Clone();
                }
            }
            catch (JsonException)
            {
                /* truncated — try next slice */
            }
        }

        return null;
    }

    private static JsonElement Unwrap(JsonElement root)
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
                    return UnwrapProfile(inner.RootElement).Clone();
                }
                catch (JsonException)
                {
                    return root;
                }
            }
        }

        return UnwrapProfile(root);
    }

    private static JsonElement UnwrapProfile(JsonElement root)
    {
        if (root.TryGetProperty("candidate_profile", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            return nested;
        }

        if (root.TryGetProperty("profile", out var profile)
            && profile.ValueKind == JsonValueKind.Object)
        {
            return profile;
        }

        return root;
    }

    private static bool HasProfileSignal(JsonElement root) =>
        root.TryGetProperty("parseQuality", out _)
        || root.TryGetProperty("candidate_profile", out _)
        || root.TryGetProperty("skills", out _)
        || root.TryGetProperty("experience", out _)
        || root.TryGetProperty("education", out _);

    private static bool HasExtractedFields(JsonElement root) =>
        HasNonEmptyArray(root, "skills")
        || HasNonEmptyArray(root, "experience")
        || HasNonEmptyArray(root, "education")
        || HasNonEmptyObject(root, "professional_summary")
        || HasNonEmptyObject(root, "personal_info");

    private static bool HasNonEmptyArray(JsonElement root, string name) =>
        root.TryGetProperty(name, out var node)
        && node.ValueKind == JsonValueKind.Array
        && node.GetArrayLength() > 0;

    private static bool HasNonEmptyObject(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var node))
        {
            return false;
        }

        if (node.ValueKind == JsonValueKind.String)
        {
            return !string.IsNullOrWhiteSpace(node.GetString());
        }

        return node.ValueKind == JsonValueKind.Object && node.EnumerateObject().Any();
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

    private static string Normalize(string content)
    {
        var trimmed = content.Trim();
        var fenceStart = trimmed.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
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

            trimmed = after.Trim();
        }

        return trimmed;
    }

    private static string? ReadString(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }
}
