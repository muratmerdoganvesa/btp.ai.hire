using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireLens.AiGateway.Providers;

public sealed class SapAiCoreOptions
{
    public const string SectionName = "SapAiCore";

    public string? ServiceKeyJson { get; set; }

    public string? DeploymentId { get; set; }

    public string ResourceGroup { get; set; } = "default";
}

public sealed record AiCoreBinding(string TokenUrl, string ClientId, string ClientSecret, string AiApiUrl);

public sealed class SapOrchestrationProvider(
    HttpClient httpClient,
    IOptions<SapAiCoreOptions> options,
    ILogger<SapOrchestrationProvider> logger) : IAiProvider
{
    public async Task<ProviderCompletion> CompleteAsync(
        MaskedPrompt prompt,
        ModelProfile profile,
        CancellationToken cancellationToken)
    {
        if (PiiMasker.ContainsUnmaskedPii(prompt.Text))
        {
            throw new InvalidOperationException("Refusing to send unmasked PII to Orchestration.");
        }

        var binding = ParseBinding(options.Value.ServiceKeyJson);
        var deploymentId = options.Value.DeploymentId
            ?? throw new InvalidOperationException("SapAiCore:DeploymentId is not configured.");

        var token = await RequestTokenAsync(binding, cancellationToken);
        var url = $"{binding.AiApiUrl.TrimEnd('/')}/v2/inference/deployments/{deploymentId}/v2/completion";

        var body = new OrchestrationCompletionRequest
        {
            Config = new OrchestrationConfig
            {
                LlmModelDetails = new LlmModelDetails
                {
                    Name = profile.ModelId,
                    Params = new Dictionary<string, object>
                    {
                        ["max_tokens"] = profile.MaxOutputTokens,
                        ["temperature"] = profile.Temperature
                    }
                },
                Template = [new OrchestrationMessage { Role = "user", Content = prompt.Text }]
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("AI-Resource-Group", options.Value.ResourceGroup);
        request.Content = JsonContent.Create(body);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Orchestration completion failed with {Status}", (int)response.StatusCode);
            throw new HttpRequestException($"Orchestration returned {(int)response.StatusCode}.");
        }

        var parsed = JsonSerializer.Deserialize<OrchestrationCompletionResponse>(raw);
        var content = parsed?.OrchestrationResult?.Choices?.FirstOrDefault()?.Message?.Content
            ?? parsed?.ModuleResults?.Llm?.Choices?.FirstOrDefault()?.Message?.Content
            ?? raw;

        return new ProviderCompletion(content, profile.ModelId, 0, 0, 0m);
    }

    public static AiCoreBinding ParseBinding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("AICORE_SERVICE_KEY / SapAiCore:ServiceKeyJson is not set.");
        }

        using var document = JsonDocument.Parse(json);
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

    private async Task<string> RequestTokenAsync(AiCoreBinding binding, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, binding.TokenUrl);
        var raw = $"{binding.ClientId}:{binding.ClientSecret}";
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("AI Core token response omitted access_token.");
    }
}
