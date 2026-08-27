using System.Diagnostics;
using System.Net.Http;
using HireLens.AiGateway;
using HireLens.Contracts.Recruiting;
using HireLens.SharedKernel;
using Microsoft.Extensions.Logging;

namespace HireLens.Modules.Recruiting.Application;

public interface ICriteriaExtractionService
{
    Task<Result<ExtractCriteriaResponse>> ExtractAsync(
        ExtractCriteriaRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Calls the hosted SAP orchestration. Prompt lives in AI Core; we only send
/// jd_title / jd_text (and aliases) and map rubric.criteria + interviewQuestions.
/// </summary>
public sealed class CriteriaExtractionService(
    IAiGateway gateway,
    ILogger<CriteriaExtractionService> logger) : ICriteriaExtractionService
{
    private const string OrchestrationId = "jd-criteria-extraction-v1";
    private const string PromptVersion = "0.0.1";
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

        var sw = Stopwatch.StartNew();
        try
        {
            var aiResult = await gateway.ExecuteAsync<string>(
                AiTaskType.CriteriaExtraction,
                new PromptContext(
                    TaskInput: $"{title}\n---\n{description}",
                    PromptVersion: PromptVersion,
                    Variables: new Dictionary<string, string>
                    {
                        ["jd_title"] = title,
                        ["jd_text"] = description,
                        ["job_title"] = title,
                        ["job_description"] = description
                    },
                    PlaceholdersOnly: true),
                new AiOptions(MaxOutputTokens: 8000, Temperature: 0),
                cancellationToken);

            sw.Stop();

            if (CriteriaExtractionMapper.IsStubContent(aiResult.Value))
            {
                logger.LogWarning(
                    "AI call orchestration={Orchestration} promptVersion={PromptVersion} model={Model} status={Status}",
                    OrchestrationId,
                    PromptVersion,
                    aiResult.ModelId,
                    "stub");
                return Result.Failure<ExtractCriteriaResponse>(
                    Error.Unavailable(
                        "AI Core bağlı değil. AICORE_SERVICE_KEY veya aicore-service-key.json olmadan kriter çıkarılamaz."));
            }

            var normalized = CriteriaExtractionMapper.Parse(aiResult.Value);

            logger.LogInformation(
                "AI call orchestration={Orchestration} promptVersion={PromptVersion} model={Model} inputTokens={InputTokens} outputTokens={OutputTokens} latencyMs={LatencyMs} status={Status} criteriaCount={CriteriaCount} interviewCount={InterviewCount} warnings={Warnings}",
                OrchestrationId,
                PromptVersion,
                aiResult.ModelId,
                aiResult.InputTokens,
                aiResult.OutputTokens,
                (long)aiResult.Latency.TotalMilliseconds,
                "ok",
                normalized.Criteria.Count,
                normalized.InterviewQuestions.Count,
                string.Join(',', aiResult.Warnings.Concat(normalized.Warnings)));

            if (normalized.Criteria.Count == 0)
            {
                logger.LogWarning(
                    "Criteria extraction returned no criteria. Preview={Preview}",
                    Truncate(aiResult.Value));
            }

            return Result.Success(normalized);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            sw.Stop();
            logger.LogWarning(
                ex,
                "AI call orchestration={Orchestration} promptVersion={PromptVersion} latencyMs={LatencyMs} status={Status}",
                OrchestrationId,
                PromptVersion,
                sw.ElapsedMilliseconds,
                "unavailable");
            return Result.Failure<ExtractCriteriaResponse>(
                Error.Unavailable(UserFacing(ex)));
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(
                ex,
                "AI call orchestration={Orchestration} promptVersion={PromptVersion} latencyMs={LatencyMs} status={Status}",
                OrchestrationId,
                PromptVersion,
                sw.ElapsedMilliseconds,
                "error");
            return Result.Failure<ExtractCriteriaResponse>(
                Error.Unavailable(UserFacing(ex)));
        }
    }

    private static bool IsServiceUnavailable(Exception ex) =>
        ex is HttpRequestException
            or TimeoutException
            or TaskCanceledException
            or InvalidOperationException;

    private static string UserFacing(Exception ex)
    {
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
