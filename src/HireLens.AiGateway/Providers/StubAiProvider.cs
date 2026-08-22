using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Routing;

namespace HireLens.AiGateway.Providers;

/// <summary>
/// Used when AICORE_SERVICE_KEY is absent. Still receives only masked text so
/// local development cannot accidentally train a habit of skipping IPiiMasker.
/// </summary>
public sealed class StubAiProvider : IAiProvider
{
    public Task<ProviderCompletion> CompleteAsync(
        MaskedPrompt prompt,
        ModelProfile profile,
        CancellationToken cancellationToken)
    {
        if (PiiMasker.ContainsUnmaskedPii(prompt.Text))
        {
            throw new InvalidOperationException("Stub provider refused unmasked PII.");
        }

        var json = """{"status":"unknown","note":"stub-provider"}""";
        return Task.FromResult(new ProviderCompletion(json, profile.ModelId, 0, 0, 0m));
    }
}
