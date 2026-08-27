using System.Diagnostics;
using System.Net.Http;
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
/// Hosted jd-criteria-extraction first (jd_title / jd_text only). If that deployment
/// rejects the call or returns an empty rubric, retry with the local prompt template
/// against the generic orchestration deployment.
/// </summary>
public sealed class CriteriaExtractionService(
    IAiGateway gateway,
    IPromptRegistry prompts,
    IOptions<SapAiCoreOptions> aiCoreOptions,
    ILogger<CriteriaExtractionService> logger) : ICriteriaExtractionService
{
    private const string OrchestrationId = "jd-criteria-extraction-v1";
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
            ["jd_text"] = description,
            ["job_title"] = title,
            ["job_description"] = description
        };
        var deploymentId = string.IsNullOrWhiteSpace(aiCoreOptions.Value.CriteriaExtractionDeploymentId)
            ? null
            : aiCoreOptions.Value.CriteriaExtractionDeploymentId;
        var localPrompt = TryGetPrompt();

        var sw = Stopwatch.StartNew();
        try
        {
            var hosted = await CallAsync(
                variables,
                placeholdersOnly: true,
                systemPrompt: null,
                userPrompt: null,
                deploymentId,
                cancellationToken);

            if (CriteriaExtractionMapper.IsStubContent(hosted.Value))
            {
                return StubFailure(hosted.ModelId);
            }

            var normalized = CriteriaExtractionMapper.Parse(hosted.Value);
            if (normalized.Criteria.Count > 0)
            {
                LogOk(hosted, normalized, sw);
                return Result.Success(normalized);
            }

            logger.LogWarning(
                "Hosted extraction returned no criteria; retrying with local prompt. Preview={Preview}",
                Truncate(hosted.Value));

            if (localPrompt is null)
            {
                LogOk(hosted, normalized, sw);
                return Result.Success(normalized);
            }

            var fallback = await CallAsync(
                variables,
                placeholdersOnly: false,
                systemPrompt: localPrompt.SystemPrompt,
                userPrompt: localPrompt.UserTemplate,
                deploymentId: null,
                cancellationToken);

            if (CriteriaExtractionMapper.IsStubContent(fallback.Value))
            {
                return StubFailure(fallback.ModelId);
            }

            normalized = CriteriaExtractionMapper.Parse(fallback.Value);
            LogOk(fallback, normalized, sw);
            return Result.Success(normalized);
        }
        catch (Exception ex) when (IsHostedRejected(ex) && localPrompt is not null)
        {
            logger.LogWarning(
                ex,
                "Hosted orchestration rejected the call; retrying with local prompt.");
            try
            {
                var fallback = await CallAsync(
                    variables,
                    placeholdersOnly: false,
                    systemPrompt: localPrompt.SystemPrompt,
                    userPrompt: localPrompt.UserTemplate,
                    deploymentId: null,
                    cancellationToken);

                if (CriteriaExtractionMapper.IsStubContent(fallback.Value))
                {
                    return StubFailure(fallback.ModelId);
                }

                var normalized = CriteriaExtractionMapper.Parse(fallback.Value);
                LogOk(fallback, normalized, sw);
                return Result.Success(normalized);
            }
            catch (Exception fallbackEx)
            {
                return Fail(fallbackEx, sw);
            }
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return Fail(ex, sw);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Criteria extraction failed");
            return Fail(ex, sw);
        }
    }

    private async Task<AiResult<string>> CallAsync(
        Dictionary<string, string> variables,
        bool placeholdersOnly,
        string? systemPrompt,
        string? userPrompt,
        string? deploymentId,
        CancellationToken cancellationToken) =>
        await gateway.ExecuteAsync<string>(
            AiTaskType.CriteriaExtraction,
            new PromptContext(
                TaskInput: $"{variables["jd_title"]}\n---\n{variables["jd_text"]}",
                PromptVersion: PromptVersion,
                Variables: variables,
                SystemPrompt: systemPrompt,
                UserPrompt: userPrompt,
                PlaceholdersOnly: placeholdersOnly,
                DeploymentId: deploymentId),
            new AiOptions(MaxOutputTokens: 8000, Temperature: 0),
            cancellationToken);

    private PromptDefinition? TryGetPrompt()
    {
        try
        {
            return prompts.Get(PromptId, PromptVersion);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Local CriteriaExtraction prompt was not loaded.");
            return null;
        }
    }

    private void LogOk(AiResult<string> aiResult, ExtractCriteriaResponse normalized, Stopwatch sw)
    {
        sw.Stop();
        logger.LogInformation(
            "AI call orchestration={Orchestration} promptVersion={PromptVersion} model={Model} inputTokens={InputTokens} outputTokens={OutputTokens} latencyMs={LatencyMs} status={Status} criteriaCount={CriteriaCount} interviewCount={InterviewCount}",
            OrchestrationId,
            PromptVersion,
            aiResult.ModelId,
            aiResult.InputTokens,
            aiResult.OutputTokens,
            sw.ElapsedMilliseconds,
            "ok",
            normalized.Criteria.Count,
            normalized.InterviewQuestions.Count);
    }

    private Result<ExtractCriteriaResponse> StubFailure(string modelId)
    {
        logger.LogWarning(
            "AI call orchestration={Orchestration} model={Model} status={Status}",
            OrchestrationId,
            modelId,
            "stub");
        return Result.Failure<ExtractCriteriaResponse>(
            Error.Unavailable(
                "AI Core bağlı değil. AICORE_SERVICE_KEY veya aicore-service-key.json olmadan kriter çıkarılamaz."));
    }

    private Result<ExtractCriteriaResponse> Fail(Exception ex, Stopwatch sw)
    {
        if (sw.IsRunning)
        {
            sw.Stop();
        }

        logger.LogWarning(
            ex,
            "AI call orchestration={Orchestration} latencyMs={LatencyMs} status={Status}",
            OrchestrationId,
            sw.ElapsedMilliseconds,
            "unavailable");
        return Result.Failure<ExtractCriteriaResponse>(Error.Unavailable(UserFacing(ex)));
    }

    private static bool IsHostedRejected(Exception ex) =>
        ex is AiCoreNonRetryableException { StatusCode: 400 }
        || (ex is HttpRequestException && ex.Message.Contains("400", StringComparison.Ordinal));

    private static bool IsServiceUnavailable(Exception ex) =>
        ex is HttpRequestException
            or TimeoutException
            or TaskCanceledException
            or InvalidOperationException
            or AiCoreNonRetryableException;

    private static string UserFacing(Exception ex)
    {
        if (ex is AiCoreNonRetryableException { StatusCode: 401 or 403 }
            || ex.Message.Contains("401", StringComparison.Ordinal)
            || ex.Message.Contains("Authentication is required", StringComparison.OrdinalIgnoreCase))
        {
            return "AI Core kimliği doğrulanamadı. AICORE_SERVICE_KEY ve resource group ayarını kontrol edin.";
        }

        var detail = ex.Message;
        if (string.IsNullOrWhiteSpace(detail))
        {
            return "Servis yanıt vermiyor. Kriterleri elle girebilirsiniz.";
        }

        return $"Servis yanıt vermiyor. Kriterleri elle girebilirsiniz. ({Truncate(detail)})";
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
