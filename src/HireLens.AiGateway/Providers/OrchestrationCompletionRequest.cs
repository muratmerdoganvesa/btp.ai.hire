using System.Text.Json.Serialization;

namespace HireLens.AiGateway.Providers;

/// <summary>
/// Isolated wire contract for SAP AI Core Orchestration v2.
/// Live curl may require a field rename here — nowhere else.
/// Endpoint: POST {aiApiUrl}/v2/inference/deployments/{deploymentId}/v2/completion
/// </summary>
public sealed class OrchestrationCompletionRequest
{
    [JsonPropertyName("config")]
    public OrchestrationConfig Config { get; init; } = new();

    [JsonPropertyName("input_params")]
    public Dictionary<string, string> InputParams { get; init; } = [];
}

public sealed class OrchestrationConfig
{
    [JsonPropertyName("LLMModelDetails")]
    public LlmModelDetails LlmModelDetails { get; init; } = new();

    [JsonPropertyName("template")]
    public List<OrchestrationMessage> Template { get; init; } = [];
}

public sealed class LlmModelDetails
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public Dictionary<string, object> Params { get; init; } = [];
}

public sealed class OrchestrationMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

public sealed class OrchestrationCompletionResponse
{
    [JsonPropertyName("orchestration_result")]
    public OrchestrationResult? OrchestrationResult { get; init; }

    [JsonPropertyName("module_results")]
    public JsonModuleResults? ModuleResults { get; init; }
}

public sealed class OrchestrationResult
{
    [JsonPropertyName("choices")]
    public List<OrchestrationChoice>? Choices { get; init; }
}

public sealed class OrchestrationChoice
{
    [JsonPropertyName("message")]
    public OrchestrationMessage? Message { get; init; }
}

public sealed class JsonModuleResults
{
    [JsonPropertyName("llm")]
    public LlmModuleResult? Llm { get; init; }
}

public sealed class LlmModuleResult
{
    [JsonPropertyName("choices")]
    public List<OrchestrationChoice>? Choices { get; init; }
}
