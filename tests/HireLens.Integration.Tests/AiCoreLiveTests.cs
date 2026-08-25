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
        var deployment = Environment.GetEnvironmentVariable("AICORE_DEPLOYMENT_ID") ?? "d08b1ad950db57c6";
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var binding = SapOrchestrationProvider.ParseBinding(key);
        binding.AiApiUrl.Should().NotBeNullOrWhiteSpace();
        binding.TokenUrl.Should().Contain("oauth/token");

        var options = Microsoft.Extensions.Options.Options.Create(new SapAiCoreOptions
        {
            ServiceKeyJson = key,
            DeploymentId = deployment,
            ResourceGroup = Environment.GetEnvironmentVariable("AICORE_RESOURCE_GROUP") ?? "default",
            ModelName = "anthropic--claude-4.5-haiku",
            ModelVersion = "1",
            TimeoutSeconds = 60,
            MaxRetries = 3,
            PlaceholderValuesKey = "placeholder_values"
        });

        using var http = new HttpClient();
        var tokens = new AiCoreTokenProvider(
            http,
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AiCoreTokenProvider>.Instance);
        var client = new OrchestrationClient(
            http,
            tokens,
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OrchestrationClient>.Instance);
        var provider = new SapOrchestrationProvider(
            client,
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SapOrchestrationProvider>.Instance);

        var result = await provider.CompleteAsync(
            new HireLens.AiGateway.Masking.MaskedPrompt(
                "Reply with {\"status\":\"ok\"} only.",
                new Dictionary<string, string>()),
            new HireLens.AiGateway.Routing.ModelProfile("anthropic--claude-4.5-haiku", null, 64, 0),
            CancellationToken.None);

        result.Content.Should().NotBeNullOrWhiteSpace();
    }
}
