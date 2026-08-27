using HireLens.AiGateway;
using HireLens.AiGateway.Prompts;
using HireLens.AiGateway.Providers;
using HireLens.Contracts.Recruiting;
using HireLens.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireLens.Modules.Recruiting.Application;

public interface ICriteriaExtractionService
{
    Task<Result<ExtractCriteriaResponse>> ExtractAsync(
        ExtractCriteriaRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Calls the jd-criteria-extraction-v1 orchestration deployment with jd_title / jd_text.
/// CV/matching keep using SapAiCore:DeploymentId (defaultOrchestrationConfig).
/// </summary>
public sealed class CriteriaExtractionService(
    IAiGateway gateway,
    IPromptRegistry prompts,
    IOptions<SapAiCoreOptions> aiCoreOptions,
    ILogger<CriteriaExtractionService> logger) : ICriteriaExtractionService
{
    private const string PromptId = "CriteriaExtraction";
    private const string PromptVersion = "1";
    private const int MinDescriptionLength = 100;
    private const int MaxDescriptionLength = 20_000;

    public async Task<Result<ExtractCriteriaResponse>> ExtractAsync(
        ExtractCriteriaRequest request,
        CancellationToken cancellationToken)
    {
        var title = request.JobTitle?.Trim() ?? string.Empty;
        var description = request.JobDescription?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<ExtractCriteriaResponse>(
                Error.Validation("Pozisyon başlığı gerekli."));
        }

        if (description.Length < MinDescriptionLength)
        {
            return Result.Failure<ExtractCriteriaResponse>(
                Error.Validation("İş tanımı kriter çıkarmak için çok kısa."));
        }

        if (description.Length > MaxDescriptionLength)
        {
            return Result.Failure<ExtractCriteriaResponse>(
                Error.Validation("İş tanımı çok uzun."));
        }

        var variables = new Dictionary<string, string>
        {
            ["jd_title"] = title,
            ["jd_text"] = description
        };

        var deploymentId = string.IsNullOrWhiteSpace(aiCoreOptions.Value.CriteriaExtractionDeploymentId)
            ? aiCoreOptions.Value.DeploymentId
            : aiCoreOptions.Value.CriteriaExtractionDeploymentId;

        try
        {
            var prompt = prompts.Get(PromptId, PromptVersion);
            var aiResult = await gateway.ExecuteAsync<string>(
                AiTaskType.CriteriaExtraction,
                new PromptContext(
                    TaskInput: $"{title}\n---\n{description}",
                    PromptVersion: prompt.Version,
                    Variables: variables,
                    SystemPrompt: prompt.SystemPrompt,
                    UserPrompt: prompt.UserTemplate,
                    DeploymentId: deploymentId),
                new AiOptions(MaxOutputTokens: 8000, Temperature: 0),
                cancellationToken);

            if (CriteriaExtractionMapper.IsStubContent(aiResult.Value))
            {
                logger.LogWarning("Criteria extraction hit StubAiProvider; AI Core is not configured.");
                return Result.Failure<ExtractCriteriaResponse>(
                    Error.Unavailable(
                        "AI Core bağlı değil. AICORE_SERVICE_KEY veya aicore-service-key.json olmadan kriter çıkarılamaz."));
            }

            var normalized = CriteriaExtractionMapper.Parse(aiResult.Value);
            logger.LogInformation(
                "Criteria extraction deployment={Deployment} model={Model} criteria={Criteria} questions={Questions}",
                deploymentId,
                aiResult.ModelId,
                normalized.Criteria.Count,
                normalized.InterviewQuestions.Count);

            if (normalized.Criteria.Count == 0)
            {
                logger.LogWarning(
                    "Criteria extraction returned empty rubric. Preview={Preview}",
                    Truncate(aiResult.Value));
            }

            return Result.Success(normalized);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Criteria extraction orchestration call failed");
            return Result.Failure<ExtractCriteriaResponse>(Error.Unavailable(UserFacing(ex)));
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
            ? "Servis yanıt vermiyor. Kriterleri elle girebilirsiniz."
            : $"Servis yanıt vermiyor. Kriterleri elle girebilirsiniz. ({Truncate(detail)})";
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
