using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireLens.AiGateway.Prompts;

public sealed record PromptDefinition(
    string Id,
    string Version,
    string SystemPrompt,
    string UserTemplate);

public interface IPromptRegistry
{
    PromptDefinition Get(string promptId, string? version = null);

    string Render(string userTemplate, IReadOnlyDictionary<string, string> values);
}

public interface IJsonSchemaRegistry
{
    JsonSchema Get(string schemaName);

    bool TryValidate(string schemaName, string json, out IReadOnlyList<string> errors);
}

/// <summary>
/// Loads prompts/{taskType}/{version}.md at startup. Prompt changes are versioned like code.
/// </summary>
public sealed class PromptRegistry : IPromptRegistry
{
    private static readonly Regex VersionHeader = new(
        @"^#\s+(?<id>[\w\-]+)\s+v(?<version>[\d.]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly ConcurrentDictionary<string, PromptDefinition> _prompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<PromptRegistry> _logger;

    public PromptRegistry(IHostEnvironment env, ILogger<PromptRegistry> logger)
    {
        _logger = logger;
        var root = FindRoot(env.ContentRootPath, "prompts");
        if (root is null)
        {
            _logger.LogWarning("prompts/ directory not found under {ContentRoot}", env.ContentRootPath);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var parts = relative.Split('/');
            var id = parts.Length >= 2 ? parts[0] : Path.GetFileNameWithoutExtension(file);
            var version = parts.Length >= 2
                ? Path.GetFileNameWithoutExtension(parts[^1])
                : "v1";

            var header = VersionHeader.Match(text);
            if (header.Success)
            {
                id = header.Groups["id"].Value;
                version = header.Groups["version"].Value;
            }
            else if (version.StartsWith('v'))
            {
                version = version[1..];
            }

            var system = text;
            var user = "{{?cv_text}}";
            var split = text.IndexOf("\n---\n", StringComparison.Ordinal);
            if (split > 0)
            {
                system = text[..split].Trim();
                user = text[(split + 5)..].Trim();
            }

            var key = $"{id}@{version}";
            var definition = new PromptDefinition(id, version, system, user);
            _prompts[key] = definition;
            _prompts[id] = definition;
            _logger.LogInformation("Loaded prompt {Key} from {File}", key, relative);
        }
    }

    public PromptDefinition Get(string promptId, string? version = null)
    {
        var key = version is null ? promptId : $"{promptId}@{version}";
        if (_prompts.TryGetValue(key, out var prompt))
        {
            return prompt;
        }

        throw new InvalidOperationException($"Prompt '{key}' was not found in the registry.");
    }

    public string Render(string userTemplate, IReadOnlyDictionary<string, string> values)
    {
        var result = userTemplate;
        foreach (var (k, v) in values)
        {
            result = result.Replace($"{{{{?{k}}}}}", v, StringComparison.Ordinal);
            result = result.Replace($"{{{{{k}}}}}", v, StringComparison.Ordinal);
        }

        return result;
    }

    internal static string? FindRoot(string contentRoot, string folder)
    {
        var dir = new DirectoryInfo(contentRoot);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, folder);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}

public sealed class JsonSchemaRegistry : IJsonSchemaRegistry
{
    private readonly ConcurrentDictionary<string, JsonSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<JsonSchemaRegistry> _logger;

    public JsonSchemaRegistry(IHostEnvironment env, ILogger<JsonSchemaRegistry> logger)
    {
        _logger = logger;
        var root = PromptRegistry.FindRoot(env.ContentRootPath, "schemas");
        if (root is null)
        {
            _logger.LogWarning("schemas/ directory not found under {ContentRoot}", env.ContentRootPath);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.schema.json", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file)
                .Replace(".schema.json", string.Empty, StringComparison.OrdinalIgnoreCase);
            var json = File.ReadAllText(file);
            _schemas[name] = JsonSchema.FromText(json);
            _logger.LogInformation("Loaded schema {Name}", name);
        }
    }

    public JsonSchema Get(string schemaName)
    {
        if (_schemas.TryGetValue(schemaName, out var schema))
        {
            return schema;
        }

        throw new InvalidOperationException($"Schema '{schemaName}' was not found.");
    }

    public bool TryValidate(string schemaName, string json, out IReadOnlyList<string> errors)
    {
        try
        {
            var schema = Get(schemaName);
            var node = JsonNode.Parse(json);
            var evaluation = schema.Evaluate(node);
            if (evaluation.IsValid)
            {
                errors = [];
                return true;
            }

            errors = ["Schema validation failed for " + schemaName];
            return false;
        }
        catch (Exception ex)
        {
            errors = [ex.Message];
            return false;
        }
    }
}
