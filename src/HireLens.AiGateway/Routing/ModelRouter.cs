using Microsoft.Extensions.Options;

namespace HireLens.AiGateway.Routing;

public sealed record ModelProfile(
    string ModelId,
    string? FallbackModelId,
    int MaxOutputTokens,
    double Temperature);

public sealed class AiGatewayOptions
{
    public const string SectionName = "AiGateway";

    public Dictionary<string, ModelProfile> Profiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Dictionary<string, string>> TenantModelPolicy { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ModelRouter(IOptions<AiGatewayOptions> options)
{
    public ModelProfile Resolve(AiTaskType taskType, Guid tenantId, AiOptions? requestOptions)
    {
        if (!string.IsNullOrWhiteSpace(requestOptions?.ModelOverride))
        {
            return new ModelProfile(
                requestOptions.ModelOverride,
                null,
                requestOptions.MaxOutputTokens,
                requestOptions.Temperature);
        }

        var taskKey = taskType.ToString();
        var tenantKey = tenantId.ToString();

        if (options.Value.TenantModelPolicy.TryGetValue(tenantKey, out var policy) &&
            policy.TryGetValue(taskKey, out var overrideModel) &&
            !string.IsNullOrWhiteSpace(overrideModel))
        {
            var baseline = options.Value.Profiles.GetValueOrDefault(taskKey);
            return new ModelProfile(
                overrideModel,
                baseline?.FallbackModelId,
                requestOptions?.MaxOutputTokens ?? baseline?.MaxOutputTokens ?? 1024,
                requestOptions?.Temperature ?? baseline?.Temperature ?? 0.1);
        }

        if (options.Value.Profiles.TryGetValue(taskKey, out var profile))
        {
            if (requestOptions is null)
            {
                return profile;
            }

            return new ModelProfile(
                profile.ModelId,
                profile.FallbackModelId,
                requestOptions.MaxOutputTokens,
                requestOptions.Temperature);
        }

        throw new InvalidOperationException(
            $"No model profile is configured for {taskType}. Set AiGateway:Profiles:{taskType}.");
    }
}
