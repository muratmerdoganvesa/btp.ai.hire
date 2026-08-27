using System.Text;
using System.Text.Json;

namespace HireLens.AiGateway.Providers;

/// <summary>
/// Pulls assistant text out of SAP Orchestration v2 envelopes.
/// Content may be a string, a Claude text-part array, or already-parsed JSON.
/// </summary>
public static class OrchestrationContentExtractor
{
    public static (string Content, int PromptTokens, int CompletionTokens) Extract(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (raw ?? string.Empty, 0, 0);
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            var content = TryChoices(root, "orchestration_result")
                ?? TryModule(root, "llm")
                ?? TryModule(root, "prompt_templating")
                ?? TryChoices(root, "final_result")
                ?? TryTopLevelChoices(root);

            var (promptTokens, completionTokens) = ReadUsage(root);
            return (string.IsNullOrWhiteSpace(content) ? raw : content, promptTokens, completionTokens);
        }
        catch (JsonException)
        {
            return (raw, 0, 0);
        }
    }

    private static string? TryModule(JsonElement root, string moduleName)
    {
        if (!root.TryGetProperty("module_results", out var modules)
            || modules.ValueKind != JsonValueKind.Object
            || !modules.TryGetProperty(moduleName, out var module))
        {
            return null;
        }

        return TryChoices(module, parentIsResult: true);
    }

    private static string? TryChoices(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var node))
        {
            return null;
        }

        return TryChoices(node, parentIsResult: true);
    }

    private static string? TryTopLevelChoices(JsonElement root) =>
        TryChoices(root, parentIsResult: true);

    private static string? TryChoices(JsonElement node, bool parentIsResult)
    {
        _ = parentIsResult;
        if (node.ValueKind != JsonValueKind.Object
            || !node.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var first = choices[0];
        if (first.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (first.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var fromMessage))
        {
            var text = ReadContent(fromMessage);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        if (first.TryGetProperty("content", out var fromChoice))
        {
            return ReadContent(fromChoice);
        }

        if (first.TryGetProperty("text", out var fromText))
        {
            return ReadContent(fromText);
        }

        return null;
    }

    private static string? ReadContent(JsonElement content) =>
        content.ValueKind switch
        {
            JsonValueKind.String => content.GetString(),
            JsonValueKind.Object => content.GetRawText(),
            JsonValueKind.Array => JoinParts(content),
            _ => null
        };

    private static string? JoinParts(JsonElement array)
    {
        var builder = new StringBuilder();
        foreach (var part in array.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                builder.Append(part.GetString());
                continue;
            }

            if (part.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (part.TryGetProperty("text", out var text))
            {
                builder.Append(ReadContent(text));
            }
            else if (part.TryGetProperty("content", out var nested))
            {
                builder.Append(ReadContent(nested));
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static (int PromptTokens, int CompletionTokens) ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("orchestration_result", out var orch)
            || !orch.TryGetProperty("usage", out var usage))
        {
            return (0, 0);
        }

        var prompt = usage.TryGetProperty("prompt_tokens", out var pt) && pt.TryGetInt32(out var p) ? p : 0;
        var completion = usage.TryGetProperty("completion_tokens", out var ct) && ct.TryGetInt32(out var c) ? c : 0;
        return (prompt, completion);
    }
}
