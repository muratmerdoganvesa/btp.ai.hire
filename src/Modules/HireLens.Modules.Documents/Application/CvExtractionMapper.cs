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
            using var doc = JsonDocument.Parse(StripFence(content));
            var root = doc.RootElement;
            var note = root.TryGetProperty("note", out var n) ? n.GetString() : null;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            return string.Equals(note, "stub-provider", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(status, "unknown", StringComparison.OrdinalIgnoreCase)
                    && !root.TryGetProperty("parseQuality", out _)
                    && !root.TryGetProperty("skills", out _));
        }
        catch (JsonException)
        {
            return true;
        }
    }

    public static bool IsUsable(string? content)
    {
        if (IsStubContent(content))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(StripFence(content));
            var root = UnwrapProfile(doc.RootElement);
            if (root.TryGetProperty("orchestration_result", out _)
                || root.TryGetProperty("module_results", out _))
            {
                var extracted = OrchestrationContentExtractor.Extract(content!).Content;
                if (string.IsNullOrWhiteSpace(extracted))
                {
                    return false;
                }

                using var inner = JsonDocument.Parse(StripFence(extracted));
                root = UnwrapProfile(inner.RootElement);
            }

            if (root.TryGetProperty("parseQuality", out var quality))
            {
                var value = quality.GetString();
                return value is "good" or "partial";
            }

            return HasExtractedFields(root);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonElement UnwrapProfile(JsonElement root)
    {
        if (root.TryGetProperty("candidate_profile", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            return nested;
        }

        return root;
    }

    private static bool HasExtractedFields(JsonElement root) =>
        HasNonEmptyArray(root, "skills")
        || HasNonEmptyArray(root, "experience")
        || HasNonEmptyArray(root, "education");

    private static bool HasNonEmptyArray(JsonElement root, string name) =>
        root.TryGetProperty(name, out var node)
        && node.ValueKind == JsonValueKind.Array
        && node.GetArrayLength() > 0;

    private static string StripFence(string? trimmed)
    {
        trimmed = (trimmed ?? string.Empty).Trim();
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
}
