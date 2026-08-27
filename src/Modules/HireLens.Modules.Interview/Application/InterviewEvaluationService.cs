using HireLens.AiGateway;
using HireLens.AiGateway.Providers;
using HireLens.Contracts.Interview;
using HireLens.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireLens.Modules.Interview.Application;

public interface IInterviewEvaluationService
{
    Task<Result<InterviewEvaluationResponse>> EvaluateAsync(
        EvaluateInterviewRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Calls the hosted interview-evaluation-v1 orchestration config.
/// Prompt lives in SAP AI Launchpad; this service only sends placeholder strings.
/// Uses the existing SapAiCore:DeploymentId (no new deployment).
/// </summary>
public sealed class InterviewEvaluationService(
    IAiGateway gateway,
    IOptions<SapAiCoreOptions> aiCoreOptions,
    ILogger<InterviewEvaluationService> logger) : IInterviewEvaluationService
{
    private const string PromptVersion = "1";

    public async Task<Result<InterviewEvaluationResponse>> EvaluateAsync(
        EvaluateInterviewRequest request,
        CancellationToken cancellationToken)
    {
        var placeholders = InterviewEvaluationPlaceholders.TryBuild(request);
        if (placeholders.IsFailure)
        {
            return Result.Failure<InterviewEvaluationResponse>(placeholders.Error);
        }

        var variables = placeholders.Value;
        var transcript = variables[InterviewEvaluationPlaceholders.Transcript];
        var deploymentId = aiCoreOptions.Value.DeploymentId;

        try
        {
            var aiResult = await gateway.ExecuteAsync<string>(
                AiTaskType.InterviewEvaluation,
                new PromptContext(
                    TaskInput: transcript,
                    PromptVersion: PromptVersion,
                    Variables: variables,
                    PlaceholdersOnly: true,
                    DeploymentId: deploymentId),
                new AiOptions(MaxOutputTokens: 2048, Temperature: 0.1),
                cancellationToken);

            if (InterviewEvaluationMapper.IsStubContent(aiResult.Value))
            {
                logger.LogWarning("Interview evaluation hit StubAiProvider; AI Core is not configured.");
                return Result.Failure<InterviewEvaluationResponse>(
                    Error.Unavailable(
                        "AI Core bağlı değil. AICORE_SERVICE_KEY veya aicore-service-key.json olmadan mülakat değerlendirilemez."));
            }

            var mapped = InterviewEvaluationMapper.Parse(aiResult.Value);
            logger.LogInformation(
                "Interview evaluation deployment={Deployment} model={Model} criteria={Criteria} warnings={Warnings}",
                deploymentId,
                aiResult.ModelId,
                mapped.Criteria.Count,
                mapped.Warnings.Count);

            return Result.Success(mapped);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Interview evaluation orchestration call failed");
            return Result.Failure<InterviewEvaluationResponse>(Error.Unavailable(UserFacing(ex)));
        }
    }

    private static string UserFacing(Exception ex)
    {
        if (ex.Message.Contains("401", StringComparison.Ordinal)
            || ex.Message.Contains("Authentication is required", StringComparison.OrdinalIgnoreCase))
        {
            return "AI Core kimliği doğrulanamadı. AICORE_SERVICE_KEY ve resource group ayarını kontrol edin.";
        }

        if (ex.Message.Contains("invalid start of a value", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains(AiCoreServiceKey.PowerShellCorruptionMessage, StringComparison.Ordinal)
            || (ex.Message.Contains("'$'", StringComparison.Ordinal) && ex.Message.Contains("JSON", StringComparison.OrdinalIgnoreCase)))
        {
            return AiCoreServiceKey.PowerShellCorruptionMessage;
        }

        var detail = ex.Message;
        return string.IsNullOrWhiteSpace(detail)
            ? "Servis yanıt vermiyor. Mülakat değerlendirmesi yapılamadı."
            : $"Servis yanıt vermiyor. Mülakat değerlendirmesi yapılamadı. ({Truncate(detail)})";
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= 400 ? value : value[..400] + "…";
    }
}
