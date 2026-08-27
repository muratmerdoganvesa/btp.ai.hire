using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Routing;

namespace HireLens.AiGateway.Providers;

public sealed record ProviderCompletion(
    string Content,
    string ModelId,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost);

/// <summary>4xx from AI Core — do not retry via Polly.</summary>
public sealed class AiCoreNonRetryableException(int statusCode, string message)
    : InvalidOperationException(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface IAiProvider
{
    Task<ProviderCompletion> CompleteAsync(
        MaskedPrompt prompt,
        ModelProfile profile,
        CancellationToken cancellationToken,
        OrchestrationPromptSpec? promptSpec = null);
}
