using FluentAssertions;
using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Prompts;
using HireLens.AiGateway.Providers;
using HireLens.AiGateway.Routing;
using HireLens.Contracts.Recruiting;
using HireLens.Modules.Documents.Application;
using HireLens.Modules.Interview.Application;
using HireLens.Modules.Matching.Application;
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
        var (system, user) = PromptRegistry.SplitMarkdown(promptText);

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
                ["jd_text"] = SampleJd
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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cv_extraction_default_orchestration_returns_usable_profile()
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
            ResourceGroup = "default",
            ModelName = "anthropic--claude-4.5-haiku",
            ModelVersion = "1",
            TimeoutSeconds = 90,
            MaxRetries = 2,
            PlaceholderValuesKey = "placeholder_values"
        });

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var tokens = new AiCoreTokenProvider(http, options, NullLogger<AiCoreTokenProvider>.Instance);
        var client = new OrchestrationClient(http, tokens, options, NullLogger<OrchestrationClient>.Instance);
        var provider = new SapOrchestrationProvider(client, options, NullLogger<SapOrchestrationProvider>.Instance);

        var promptPath = FindRepoFile(Path.Combine("prompts", "CvExtraction", "v1.1.0.md"));
        promptPath.Should().NotBeNull();
        var promptText = await File.ReadAllTextAsync(promptPath!);
        var (system, user) = PromptRegistry.SplitMarkdown(promptText);

        const string cvText = """
            Senior backend engineer. 6 years C# / ASP.NET Core, REST APIs, Entity Framework, SQL Server.
            Led a hiring pipeline service on SAP BTP. Docker, Kubernetes, Git. B.Sc. Computer Engineering.
            """;

        var result = await provider.CompleteAsync(
            new MaskedPrompt(cvText, new Dictionary<string, string>()),
            new ModelProfile("anthropic--claude-4.5-haiku", null, 2048, 0.1),
            CancellationToken.None,
            new OrchestrationPromptSpec(
                SystemPrompt: system,
                UserPrompt: user,
                Placeholders: new Dictionary<string, string>
                {
                    ["cv_text"] = cvText,
                    ["application_data"] = "yok"
                },
                DeploymentId: "d05d3551770ab22b"));

        result.Content.Should().NotBeNullOrWhiteSpace();
        CvExtractionMapper.IsUsable(result.Content).Should().BeTrue(
            "CV extraction AI kullanılamadı. Preview={0}",
            Truncate(result.Content));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Matching_hosted_deployment_returns_criterion_scores()
    {
        var key = LoadServiceKey();
        if (key is null)
        {
            return;
        }

        var (provider, _) = CreateProvider(key);
        var (system, user) = LoadPrompt("JdCvMatching", "v1.0.0.md");
        const string cvText = """
            Senior backend engineer. 6 years C# / ASP.NET Core, REST APIs, Entity Framework, SQL Server.
            Led a hiring pipeline service on SAP BTP. Docker, Kubernetes, Git.
            """;
        var csharp = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sql = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var position = new PositionSnapshot(
            Guid.NewGuid(),
            "Backend .NET Geliştirici",
            SampleJd,
            [
                new PositionCriterionDto(csharp, "C#", "C# ve ASP.NET Core", 60),
                new PositionCriterionDto(sql, "SQL", "SQL ve EF", 40)
            ]);

        var result = await provider.CompleteAsync(
            new MaskedPrompt(cvText, new Dictionary<string, string>()),
            new ModelProfile("anthropic--claude-4.5-haiku", null, 2048, 0.1),
            CancellationToken.None,
            new OrchestrationPromptSpec(
                SystemPrompt: system,
                UserPrompt: user,
                Placeholders: new Dictionary<string, string>
                {
                    ["jd_structured"] = """{"title":"Backend .NET Geliştirici","jobDescription":"C# ASP.NET Core SQL"}""",
                    ["rubric_criteria"] = $"[{{ \"criterionId\":\"{csharp:D}\",\"name\":\"C#\",\"weight\":60}},{{ \"criterionId\":\"{sql:D}\",\"name\":\"SQL\",\"weight\":40}}]",
                    ["candidate_profile"] = "{\"cv_text\":" + System.Text.Json.JsonSerializer.Serialize(cvText) + "}"
                },
                DeploymentId: "dcb0d6d919f15368"));

        result.Content.Should().NotBeNullOrWhiteSpace();
        CriteriaMatchingMapper.IsStubContent(result.Content).Should().BeFalse();
        var mapped = CriteriaMatchingMapper.TryMap(result.Content, position);
        mapped.Should().NotBeNull("Eşleştirme AI skor döndürmedi. Preview={0}", Truncate(result.Content));
        mapped!.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Interview_evaluation_hosted_deployment_returns_criteria()
    {
        var key = LoadServiceKey();
        if (key is null)
        {
            return;
        }

        var (provider, _) = CreateProvider(key);
        var (system, user) = LoadPrompt("InterviewEvaluation", "v1.md");
        const string transcript = "[00:03:12] Mülakatçı: C# deneyiminizi anlatır mısınız?\n[00:03:20] Aday: C# ile REST API yazdım, Entity Framework kullandım.";
        var result = await provider.CompleteAsync(
            new MaskedPrompt(transcript, new Dictionary<string, string>()),
            new ModelProfile("anthropic--claude-4.5-haiku", null, 2048, 0.1),
            CancellationToken.None,
            new OrchestrationPromptSpec(
                SystemPrompt: system,
                UserPrompt: user,
                Placeholders: new Dictionary<string, string>
                {
                    ["job_title"] = "Backend .NET Geliştirici",
                    ["rubric"] = """{"rubricId":"r1","rubricVersion":"1","language":"tr","weightTotal":100,"criteria":[{"criterionId":"csharp","name":"C#","description":"C# deneyimi","weight":100,"mandatory":true,"anchors":{"100":"uzman","70":"yeterli","40":"kısmi","0":"yok"}}]}""",
                    ["interview_questions"] = """[{"questionId":"q1","criterionId":"csharp","question":"C# deneyiminizi anlatır mısınız?","whatToListenFor":["somut API örneği"]}]""",
                    ["cv_match_result"] = "",
                    ["transcript"] = transcript
                },
                DeploymentId: "da115516a621a2e7"));

        result.Content.Should().NotBeNullOrWhiteSpace();
        InterviewEvaluationMapper.IsStubContent(result.Content).Should().BeFalse();
        var mapped = InterviewEvaluationMapper.Parse(result.Content);
        mapped.Criteria.Should().NotBeEmpty("Mülakat AI kriter döndürmedi. Preview={0}", Truncate(result.Content));
    }

    private static (SapOrchestrationProvider Provider, HttpClient Http) CreateProvider(string key)
    {
        var options = Options.Create(new SapAiCoreOptions
        {
            ServiceKeyJson = key,
            DeploymentId = "d08b1ad950db57c6",
            ResourceGroup = "default",
            ModelName = "anthropic--claude-4.5-haiku",
            ModelVersion = "1",
            TimeoutSeconds = 90,
            MaxRetries = 2,
            PlaceholderValuesKey = "placeholder_values"
        });
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var tokens = new AiCoreTokenProvider(http, options, NullLogger<AiCoreTokenProvider>.Instance);
        var client = new OrchestrationClient(http, tokens, options, NullLogger<OrchestrationClient>.Instance);
        return (new SapOrchestrationProvider(client, options, NullLogger<SapOrchestrationProvider>.Instance), http);
    }

    private static (string System, string User) LoadPrompt(string folder, string file)
    {
        var path = FindRepoFile(Path.Combine("prompts", folder, file));
        path.Should().NotBeNull();
        return PromptRegistry.SplitMarkdown(File.ReadAllText(path!));
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
