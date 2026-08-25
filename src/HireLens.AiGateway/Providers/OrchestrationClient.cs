using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireLens.AiGateway.Providers;

public sealed record OrchestrationCallResult(
    string Content,
    string ModelId,
    string? ModelVersion,
    int PromptTokens,
    int CompletionTokens,
    long LatencyMs,
    string RawResponse);

/// <summary>
/// SAP AI Core Orchestration v2 HTTP client: retry on 429/5xx, dual deserialize,
/// concurrency gate, and optional JSON-schema response_format.
/// </summary>
public sealed class OrchestrationClient(
    HttpClient httpClient,
    AiCoreTokenProvider tokens,
    IOptions<SapAiCoreOptions> options,
    ILogger<OrchestrationClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _concurrency = new(8, 8);

    public async Task<OrchestrationCallResult> CompleteAsync(
        MaskedPrompt prompt,
        ModelProfile profile,
        OrchestrationPromptSpec? promptSpec = null,
        CancellationToken cancellationToken = default)
    {
        if (PiiMasker.ContainsUnmaskedPii(prompt.Text))
        {
            throw new InvalidOperationException("Refusing to send unmasked PII to Orchestration.");
        }

        var opts = options.Value;
        var binding = tokens.ResolveBinding();
        var deploymentId = opts.DeploymentId
            ?? throw new InvalidOperationException("SapAiCore:DeploymentId is not configured.");

        var modelName = string.IsNullOrWhiteSpace(opts.ModelName) ? profile.ModelId : opts.ModelName;
        var modelVersion = string.IsNullOrWhiteSpace(opts.ModelVersion) ? "1" : opts.ModelVersion;
        var url = $"{binding.AiApiUrl.TrimEnd('/')}/v2/inference/deployments/{deploymentId}/v2/completion";

        var body = BuildRequest(prompt, promptSpec, modelName, modelVersion, profile);
        var payload = JsonSerializer.Serialize(body, JsonOptions);

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            return await SendWithRetryAsync(
                url,
                payload,
                modelName,
                modelVersion,
                opts,
                cancellationToken);
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async Task<OrchestrationCallResult> SendWithRetryAsync(
        string url,
        string payload,
        string modelName,
        string modelVersion,
        SapAiCoreOptions opts,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, opts.MaxRetries);
        Exception? last = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var token = await tokens.GetTokenAsync(cancellationToken);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.TryAddWithoutValidation("AI-Resource-Group", opts.ResourceGroup);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds)));

                using var response = await httpClient.SendAsync(request, cts.Token);
                var raw = await response.Content.ReadAsStringAsync(cts.Token);
                sw.Stop();

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    logger.LogWarning("Orchestration 400 (no retry): {Body}", Truncate(raw));
                    throw new HttpRequestException($"Orchestration returned 400: {Truncate(raw)}");
                }

                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                {
                    last = new HttpRequestException($"Orchestration returned {(int)response.StatusCode}.");
                    if (attempt < maxAttempts)
                    {
                        var delay = TimeSpan.FromSeconds(attempt * attempt);
                        logger.LogWarning(
                            "Orchestration {Status} on attempt {Attempt}; backing off {Delay}s",
                            (int)response.StatusCode,
                            attempt,
                            delay.TotalSeconds);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    throw last;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Orchestration returned {(int)response.StatusCode}: {Truncate(raw)}");
                }

                var (content, promptTokens, completionTokens) = ExtractContent(raw);
                return new OrchestrationCallResult(
                    content,
                    modelName,
                    modelVersion,
                    promptTokens,
                    completionTokens,
                    sw.ElapsedMilliseconds,
                    raw);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts && !ex.Message.Contains("400"))
            {
                last = ex;
                var delay = TimeSpan.FromSeconds(attempt * attempt);
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                last = ex;
                var delay = TimeSpan.FromSeconds(attempt * attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw last ?? new HttpRequestException("Orchestration call failed after retries.");
    }

    private object BuildRequest(
        MaskedPrompt prompt,
        OrchestrationPromptSpec? promptSpec,
        string modelName,
        string modelVersion,
        ModelProfile profile)
    {
        var systemContent = promptSpec?.SystemPrompt ?? string.Empty;
        var userContent = promptSpec?.UserPrompt ?? prompt.Text;
        var placeholders = promptSpec?.Placeholders ?? new Dictionary<string, string>
        {
            ["cv_text"] = prompt.Text
        };

        var template = new List<OrchestrationMessage>();
        if (!string.IsNullOrWhiteSpace(systemContent))
        {
            template.Add(new OrchestrationMessage { Role = "system", Content = systemContent });
        }

        template.Add(new OrchestrationMessage { Role = "user", Content = userContent });

        var promptNode = new Dictionary<string, object?>
        {
            ["template"] = template,
            ["defaults"] = promptSpec?.Defaults ?? new Dictionary<string, string> { ["application_data"] = "yok" }
        };

        if (promptSpec?.ResponseSchema is not null)
        {
            promptNode["response_format"] = new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["json_schema"] = new Dictionary<string, object?>
                {
                    ["name"] = promptSpec.SchemaName ?? "structured_output",
                    ["strict"] = true,
                    ["schema"] = promptSpec.ResponseSchema
                }
            };
        }

        var modules = new Dictionary<string, object?>
        {
            ["prompt_templating"] = new Dictionary<string, object?>
            {
                ["prompt"] = promptNode,
                ["model"] = new Dictionary<string, object?>
                {
                    ["name"] = modelName,
                    ["version"] = modelVersion,
                    ["params"] = new Dictionary<string, object>
                    {
                        ["max_tokens"] = profile.MaxOutputTokens,
                        ["temperature"] = profile.Temperature
                    }
                }
            }
        };

        var root = new Dictionary<string, object?>
        {
            ["config"] = new Dictionary<string, object?> { ["modules"] = modules }
        };

        var placeholderKey = string.IsNullOrWhiteSpace(options.Value.PlaceholderValuesKey)
            ? "placeholder_values"
            : options.Value.PlaceholderValuesKey;
        root[placeholderKey] = placeholders;
        return root;
    }

    private static (string Content, int PromptTokens, int CompletionTokens) ExtractContent(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        string? content = null;
        if (root.TryGetProperty("orchestration_result", out var orch)
            && orch.TryGetProperty("choices", out var choices)
            && choices.GetArrayLength() > 0)
        {
            content = choices[0].GetProperty("message").GetProperty("content").GetString();
        }

        if (content is null
            && root.TryGetProperty("module_results", out var modules)
            && modules.TryGetProperty("llm", out var llm)
            && llm.TryGetProperty("choices", out var llmChoices)
            && llmChoices.GetArrayLength() > 0)
        {
            content = llmChoices[0].GetProperty("message").GetProperty("content").GetString();
        }

        content ??= raw;

        // content may itself be a JSON string — second deserialize stays with the caller
        var promptTokens = 0;
        var completionTokens = 0;
        if (root.TryGetProperty("orchestration_result", out var orch2)
            && orch2.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt))
            {
                promptTokens = pt.GetInt32();
            }

            if (usage.TryGetProperty("completion_tokens", out var ct))
            {
                completionTokens = ct.GetInt32();
            }
        }

        return (content, promptTokens, completionTokens);
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "…";
}

public sealed record OrchestrationPromptSpec(
    string? SystemPrompt,
    string UserPrompt,
    IReadOnlyDictionary<string, string> Placeholders,
    JsonElement? ResponseSchema = null,
    string? SchemaName = null,
    IReadOnlyDictionary<string, string>? Defaults = null);
