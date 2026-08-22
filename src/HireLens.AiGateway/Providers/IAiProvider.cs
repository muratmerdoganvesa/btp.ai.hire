using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Routing;

namespace HireLens.AiGateway.Providers;

public sealed record ProviderCompletion(
    string Content,
    string ModelId,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost);

public interface IAiProvider
{
    Task<ProviderCompletion> CompleteAsync(
        MaskedPrompt prompt,
        ModelProfile profile,
        CancellationToken cancellationToken);
}
