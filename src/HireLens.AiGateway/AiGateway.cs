using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Providers;
using HireLens.AiGateway.Routing;
using HireLens.Infrastructure.Persistence;
using HireLens.SharedKernel;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace HireLens.AiGateway;

public sealed class AiGateway(
    IPiiMasker masker,
    IAiProvider provider,
    ModelRouter router,
    ITenantContext tenantContext,
    IClock clock,
    HireLensDbContext db) : IAiGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>().Handle<TimeoutException>()
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 8,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
        })
        .Build();

    public async Task<AiResult<T>> ExecuteAsync<T>(
        AiTaskType taskType,
        PromptContext context,
        AiOptions? options = null,
        CancellationToken ct = default)
    {
        if (!tenantContext.IsResolved)
        {
            throw new InvalidOperationException("AI Gateway refuses to run without a tenant context.");
        }

        var masked = masker.Mask(context.TaskInput);
        if (PiiMasker.ContainsUnmaskedPii(masked.Text))
        {
            throw new InvalidOperationException("Masked prompt still contained PII.");
        }

        IReadOnlyDictionary<string, string>? maskedVariables = null;
        if (context.Variables is { Count: > 0 })
        {
            maskedVariables = context.Variables.ToDictionary(
                pair => pair.Key,
                pair => masker.Mask(pair.Value).Text,
                StringComparer.Ordinal);
        }

        OrchestrationPromptSpec? promptSpec = null;
        if (context.PlaceholdersOnly)
        {
            promptSpec = new OrchestrationPromptSpec(
                SystemPrompt: null,
                UserPrompt: string.Empty,
                Placeholders: maskedVariables ?? new Dictionary<string, string>(StringComparer.Ordinal),
                PlaceholdersOnly: true,
                DeploymentId: context.DeploymentId);
        }
        else if (!string.IsNullOrWhiteSpace(context.SystemPrompt) || !string.IsNullOrWhiteSpace(context.UserPrompt))
        {
            promptSpec = new OrchestrationPromptSpec(
                context.SystemPrompt,
                context.UserPrompt ?? masked.Text,
                maskedVariables ?? new Dictionary<string, string>(StringComparer.Ordinal),
                DeploymentId: context.DeploymentId);
        }

        var profile = router.Resolve(taskType, tenantContext.TenantId, options);
        var started = clock.UtcNow;
        var warnings = new List<string>();

        ProviderCompletion completion;
        try
        {
            completion = await _pipeline.ExecuteAsync(
                async token => await provider.CompleteAsync(masked, profile, token, promptSpec),
                ct);
        }
        catch (Exception ex) when (
            !string.IsNullOrWhiteSpace(profile.FallbackModelId)
            && ex is not AiCoreNonRetryableException)
        {
            var fallback = profile with { ModelId = profile.FallbackModelId! };
            completion = await provider.CompleteAsync(masked, fallback, ct, promptSpec);
            warnings.Add("fallback_model");
        }

        var value = DeserializeOrUnknown<T>(completion.Content, warnings);
        var latency = clock.UtcNow - started;

        db.AiInvocations.Add(new AiInvocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            TaskType = taskType.ToString(),
            ModelId = completion.ModelId,
            PromptVersion = context.PromptVersion,
            PromptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(masked.Text))),
            InputTokens = completion.InputTokens,
            OutputTokens = completion.OutputTokens,
            EstimatedCost = completion.EstimatedCost,
            LatencyMs = (long)latency.TotalMilliseconds,
            CorrelationId = tenantContext.CorrelationId,
            OccurredAt = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return new AiResult<T>(
            value,
            completion.ModelId,
            context.PromptVersion,
            completion.InputTokens,
            completion.OutputTokens,
            completion.EstimatedCost,
            latency,
            null,
            warnings);
    }

    private static T DeserializeOrUnknown<T>(string content, List<string> warnings)
    {
        if (TryDeserialize<T>(content, out var first) && first is not null)
        {
            return first;
        }

        if (TryDeserialize<T>(content, out var second) && second is not null)
        {
            return second;
        }

        warnings.Add("schema_mismatch_unknown");
        if (typeof(T) == typeof(string))
        {
            return (T)(object)content;
        }

        if (TryDeserialize<T>("{}", out var empty) && empty is not null)
        {
            return empty;
        }

        throw new InvalidOperationException("Structured output did not match the requested schema.");
    }

    private static bool TryDeserialize<T>(string content, out T? value)
    {
        var normalized = NormalizeJsonContent(content);
        try
        {
            value = JsonSerializer.Deserialize<T>(normalized, JsonOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }

    private static string NormalizeJsonContent(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            var fence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                trimmed = trimmed[..fence];
            }

            trimmed = trimmed.Trim();
        }

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            try
            {
                var unquoted = JsonSerializer.Deserialize<string>(trimmed);
                if (!string.IsNullOrWhiteSpace(unquoted))
                {
                    return unquoted.Trim();
                }
            }
            catch (JsonException)
            {
                /* keep original */
            }
        }

        return trimmed;
    }
}
