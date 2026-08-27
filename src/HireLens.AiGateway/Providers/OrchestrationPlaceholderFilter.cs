using System.Text.RegularExpressions;

namespace HireLens.AiGateway.Providers;

/// <summary>
/// SAP generic orchestration 400s when placeholder_values / defaults contain
/// keys that are not referenced as {{?name}} in the prompt template.
/// </summary>
public static class OrchestrationPlaceholderFilter
{
    private static readonly Regex Token = new(
        @"\{\{\?(?<n>[A-Za-z0-9_]+)\}\}",
        RegexOptions.Compiled);

    public static IReadOnlySet<string> NamesIn(string? template)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(template))
        {
            return names;
        }

        foreach (Match match in Token.Matches(template))
        {
            names.Add(match.Groups["n"].Value);
        }

        return names;
    }

    public static Dictionary<string, string> ForTemplate(
        string? template,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string>? defaults = null)
    {
        var names = NamesIn(template);
        if (names.Count == 0)
        {
            return new Dictionary<string, string>(values, StringComparer.Ordinal);
        }

        var bag = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (values.TryGetValue(name, out var value))
            {
                bag[name] = value;
            }
            else if (defaults is not null && defaults.TryGetValue(name, out var fallback))
            {
                bag[name] = fallback;
            }
            else if (name == "application_data")
            {
                bag[name] = "yok";
            }
        }

        return bag;
    }
}
