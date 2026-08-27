using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireLens.AiGateway.Providers;

public sealed class SapAiCoreOptions
{
    public const string SectionName = "SapAiCore";

    /// <summary>Full AI Core service-key JSON. Preferred over discrete ClientId/Secret fields.</summary>
    public string? ServiceKeyJson { get; set; }

    /// <summary>Optional path to a gitignored aicore-service-key.json (local dev).</summary>
    public string? ServiceKeyPath { get; set; }

    public string? AiApiUrl { get; set; }

    public string? XsuaaUrl { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? DeploymentId { get; set; }

    /// <summary>Optional dedicated deployment for jd-criteria-extraction-v1.</summary>
    public string? CriteriaExtractionDeploymentId { get; set; }

    public string ResourceGroup { get; set; } = "default";

    public string ModelName { get; set; } = "anthropic--claude-4.5-haiku";

    public string ModelVersion { get; set; } = "1";

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Orchestration placeholder bag key. Verified landscapes use "placeholder_values";
    /// some older deployments expect "input_params". Flip after a 400 on first call.
    /// </summary>
    public string PlaceholderValuesKey { get; set; } = "placeholder_values";
}

public sealed record AiCoreBinding(string TokenUrl, string ClientId, string ClientSecret, string AiApiUrl);

public sealed class SapOrchestrationProvider(
    OrchestrationClient client,
    IOptions<SapAiCoreOptions> options,
    ILogger<SapOrchestrationProvider> logger) : IAiProvider
{
    public async Task<ProviderCompletion> CompleteAsync(
        MaskedPrompt prompt,
        ModelProfile profile,
        CancellationToken cancellationToken,
        OrchestrationPromptSpec? promptSpec = null)
    {
        var result = await client.CompleteAsync(prompt, profile, promptSpec, cancellationToken);
        logger.LogDebug(
            "Orchestration completed model={Model} v={Version} promptTokens={Prompt} completionTokens={Completion} latencyMs={Latency}",
            result.ModelId,
            result.ModelVersion ?? options.Value.ModelVersion,
            result.PromptTokens,
            result.CompletionTokens,
            result.LatencyMs);

        return new ProviderCompletion(
            result.Content,
            result.ModelId,
            result.PromptTokens,
            result.CompletionTokens,
            0m);
    }

    public static AiCoreBinding ParseBinding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("AICORE_SERVICE_KEY / SapAiCore:ServiceKeyJson is not set.");
        }

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        var clientId = root.GetProperty("clientid").GetString()
            ?? throw new InvalidOperationException("AI Core binding omitted clientid.");
        var clientSecret = root.GetProperty("clientsecret").GetString()
            ?? throw new InvalidOperationException("AI Core binding omitted clientsecret.");
        var tokenUrl = root.GetProperty("url").GetString()
            ?? throw new InvalidOperationException("AI Core binding omitted url.");
        if (!tokenUrl.Contains("oauth/token", StringComparison.OrdinalIgnoreCase))
        {
            tokenUrl = tokenUrl.TrimEnd('/') + "/oauth/token";
        }

        var aiApi = root.GetProperty("serviceurls").GetProperty("AI_API_URL").GetString()
            ?? throw new InvalidOperationException("AI Core binding omitted serviceurls.AI_API_URL.");

        return new AiCoreBinding(tokenUrl, clientId, clientSecret, aiApi);
    }
}
