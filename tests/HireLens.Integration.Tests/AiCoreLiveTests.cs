using FluentAssertions;
using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Providers;
using HireLens.AiGateway.Routing;
using HireLens.Modules.Recruiting.Application;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HireLens.Integration.Tests;

public sealed class AiCoreLiveTests
{
    private const string SampleJd = """
        Backend .NET geliştirici arıyoruz. En az 4 yıl C# ve ASP.NET Core deneyimi şarttır.
        REST API tasarımı, Entity Framework, SQL ve Git zorunludur. Docker ve Kubernetes
        bilgisi tercih edilir. Scrum ekibinde çalışacak, code review yapacak ve üretim
        ortamındaki olaylara müdahale edecektir. Uzaktan çalışma mümkündür. İstanbul ofisi vardır.
        """;

    [Fact]
    [Trait("Category", "Integration")]
    public void Local_service_key_file_parses_including_dollar_in_secret()
    {
        var key = LoadServiceKey();
        if (key is null)
        {
            return;
        }

        var binding = SapOrchestrationProvider.ParseBinding(key);
        binding.ClientSecret.Should().NotBeNullOrWhiteSpace();
        binding.TokenUrl.Should().Contain("oauth/token");
        binding.AiApiUrl.Should().Contain("hana.ondemand.com");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Criteria_extraction_returns_weighted_rubric_from_ai_core()
    {
        var key = LoadServiceKey();
        if (key is null)
        {
            return;
        }

        var options = Options.Create(new SapAiCoreOptions
        {
            ServiceKeyJson = key,
            DeploymentId = "d08b1ad950db57c6",
            CriteriaExtractionDeploymentId = "dbec6f896a57c947",
            ResourceGroup = "default",
            ModelName = "anthropic--claude-4.5-haiku",
            ModelVersion = "1",
            TimeoutSeconds = 90,
            MaxRetries = 2,
            PlaceholderValuesKey = "placeholder_values"
        });

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var tokens = new AiCoreTokenProvider(
            http,
            options,
            NullLogger<AiCoreTokenProvider>.Instance);

        var token = await tokens.GetTokenAsync(CancellationToken.None);
        token.Should().NotBeNullOrWhiteSpace("XSUAA token alınamadı");

        var promptPath = FindRepoFile(Path.Combine("prompts", "CriteriaExtraction", "v1.md"));
        promptPath.Should().NotBeNull();
        var promptText = await File.ReadAllTextAsync(promptPath!);
        var split = promptText.IndexOf("\n---\n", StringComparison.Ordinal);
        var system = split > 0 ? promptText[..split].Trim() : promptText;
        var user = split > 0 ? promptText[(split + 5)..].Trim() : "{{?jd_text}}";

        var client = new OrchestrationClient(
            http,
            tokens,
            options,
            NullLogger<OrchestrationClient>.Instance);
        var provider = new SapOrchestrationProvider(
            client,
            options,
            NullLogger<SapOrchestrationProvider>.Instance);

        var spec = new OrchestrationPromptSpec(
            SystemPrompt: system,
            UserPrompt: user,
            Placeholders: new Dictionary<string, string>
            {
                ["jd_title"] = "Backend .NET Geliştirici",
                ["jd_text"] = SampleJd,
                ["job_title"] = "Backend .NET Geliştirici",
                ["job_description"] = SampleJd
            },
            DeploymentId: "dbec6f896a57c947");

        var result = await provider.CompleteAsync(
            new MaskedPrompt(SampleJd, new Dictionary<string, string>()),
            new ModelProfile("anthropic--claude-4.5-haiku", null, 8000, 0),
            CancellationToken.None,
            spec);

        result.Content.Should().NotBeNullOrWhiteSpace();
        CriteriaExtractionMapper.IsStubContent(result.Content).Should().BeFalse();

        var mapped = CriteriaExtractionMapper.Parse(result.Content);
        mapped.Criteria.Should().NotBeEmpty("AI Core kriter döndürmedi. Preview={0}", Truncate(result.Content));
        mapped.TotalWeight.Should().Be(100);
    }

    private static string? LoadServiceKey()
    {
        var env = Environment.GetEnvironmentVariable("AICORE_SERVICE_KEY");
        var file = FindRepoFile("aicore-service-key.json");
        var fileJson = file is not null ? File.ReadAllText(file) : null;
        return AiCoreServiceKey.Coalesce(env, fileJson);
    }

    private static string? FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var direct = Path.Combine(dir.FullName, relative);
            if (File.Exists(direct))
            {
                return direct;
            }

            var nested = Path.Combine(dir.FullName, "hirelens", relative);
            if (File.Exists(nested))
            {
                return nested;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";
}
