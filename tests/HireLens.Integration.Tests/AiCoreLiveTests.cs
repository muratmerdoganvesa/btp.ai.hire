using FluentAssertions;
using HireLens.AiGateway.Providers;
using Xunit;

namespace HireLens.Integration.Tests;

public sealed class AiCoreLiveTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Orchestration_token_and_completion_succeed_when_service_key_is_present()
    {
        var key = Environment.GetEnvironmentVariable("AICORE_SERVICE_KEY");
        var deployment = Environment.GetEnvironmentVariable("AICORE_DEPLOYMENT_ID");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(deployment))
        {
            return;
        }

        var binding = SapOrchestrationProvider.ParseBinding(key);
        binding.AiApiUrl.Should().NotBeNullOrWhiteSpace();
        binding.TokenUrl.Should().Contain("oauth/token");

        using var http = new HttpClient();
        var provider = new SapOrchestrationProvider(
            http,
            Microsoft.Extensions.Options.Options.Create(new SapAiCoreOptions
            {
                ServiceKeyJson = key,
                DeploymentId = deployment,
                ResourceGroup = Environment.GetEnvironmentVariable("AICORE_RESOURCE_GROUP") ?? "default"
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SapOrchestrationProvider>.Instance);

        var result = await provider.CompleteAsync(
            new HireLens.AiGateway.Masking.MaskedPrompt("Reply with {\"status\":\"ok\"} only.", new Dictionary<string, string>()),
            new HireLens.AiGateway.Routing.ModelProfile("gpt-4o-mini", null, 32, 0),
            CancellationToken.None);

        result.Content.Should().NotBeNullOrWhiteSpace();
    }
}
