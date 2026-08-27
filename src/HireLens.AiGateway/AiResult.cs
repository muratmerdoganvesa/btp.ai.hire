namespace HireLens.AiGateway;

public sealed record AiResult<T>(
    T Value,
    string ModelId,
    string PromptVersion,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost,
    TimeSpan Latency,
    double? Confidence,
    IReadOnlyList<string> Warnings);

public sealed record PromptContext(
    string TaskInput,
    string PromptVersion,
    IReadOnlyDictionary<string, string>? Variables = null,
    string? SystemPrompt = null,
    string? UserPrompt = null,
    /// <summary>
    /// When true, only placeholder_values are sent; the deployment's own prompt template is used.
    /// </summary>
    bool PlaceholdersOnly = false,
    string? DeploymentId = null);

public sealed record AiOptions(
    string? ModelOverride = null,
    int MaxOutputTokens = 1024,
    double Temperature = 0.1);
