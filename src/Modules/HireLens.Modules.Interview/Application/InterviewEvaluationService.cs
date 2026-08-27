using HireLens.AiGateway;
using HireLens.AiGateway.Prompts;
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
/// Calls interview-evaluation-v1 like jd-criteria-extraction-v1:
/// dedicated deployment + repo prompt in config + placeholder values.
/// </summary>
public sealed class InterviewEvaluationService(
    IAiGateway gateway,
    IPromptRegistry prompts,
    IOptions<SapAiCoreOptions> aiCoreOptions,
    ILogger<InterviewEvaluationService> logger) : IInterviewEvaluationService
{
    private const string PromptId = "InterviewEvaluation";
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
        var deploymentId = string.IsNullOrWhiteSpace(aiCoreOptions.Value.InterviewEvaluationDeploymentId)
            ? aiCoreOptions.Value.DeploymentId
            : aiCoreOptions.Value.InterviewEvaluationDeploymentId;
        var prompt = prompts.Get(PromptId, PromptVersion);

        try
        {
            var aiResult = await gateway.ExecuteAsync<string>(
                AiTaskType.InterviewEvaluation,
                new PromptContext(
                    TaskInput: transcript,
                    PromptVersion: prompt.Version,
                    Variables: variables,
                    SystemPrompt: prompt.SystemPrompt,
                    UserPrompt: prompt.UserTemplate,
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
