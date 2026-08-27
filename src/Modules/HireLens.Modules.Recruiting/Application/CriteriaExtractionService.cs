using System.Diagnostics;
using System.Net.Http;
using HireLens.AiGateway;
using HireLens.AiGateway.Prompts;
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
/// Sends jd_title / jd_text into orchestration (generic deployment gets the local
/// prompt template; a scenario deployment still receives the same placeholders).
/// </summary>
public sealed class CriteriaExtractionService(
    IAiGateway gateway,
    IPromptRegistry prompts,
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

        var prompt = prompts.Get(PromptId, PromptVersion);
        var variables = new Dictionary<string, string>
        {
            ["jd_title"] = title,
            ["jd_text"] = description,
            ["job_title"] = title,
            ["job_description"] = description
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var aiResult = await gateway.ExecuteAsync<string>(
                AiTaskType.CriteriaExtraction,
                new PromptContext(
                    TaskInput: $"{title}\n---\n{description}",
                    PromptVersion: prompt.Version,
                    Variables: variables,
                    SystemPrompt: prompt.SystemPrompt,
                    UserPrompt: prompt.UserTemplate),
                new AiOptions(MaxOutputTokens: 8000, Temperature: 0),
                cancellationToken);

            sw.Stop();

            if (CriteriaExtractionMapper.IsStubContent(aiResult.Value))
            {
                logger.LogWarning(
                    "AI call prompt={PromptId}@{PromptVersion} model={Model} status={Status}",
                    PromptId,
                    prompt.Version,
                    aiResult.ModelId,
                    "stub");
                return Result.Failure<ExtractCriteriaResponse>(
                    Error.Unavailable("Servis yanıt vermiyor. Kriterleri elle girebilirsiniz."));
            }

            var normalized = CriteriaExtractionMapper.Parse(aiResult.Value);

            logger.LogInformation(
                "AI call prompt={PromptId}@{PromptVersion} model={Model} inputTokens={InputTokens} outputTokens={OutputTokens} latencyMs={LatencyMs} status={Status} criteriaCount={CriteriaCount} interviewCount={InterviewCount} warnings={Warnings}",
                PromptId,
                prompt.Version,
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
                "AI call prompt={PromptId}@{PromptVersion} latencyMs={LatencyMs} status={Status}",
                PromptId,
                PromptVersion,
                sw.ElapsedMilliseconds,
                "unavailable");
            return Result.Failure<ExtractCriteriaResponse>(
                Error.Unavailable("Servis yanıt vermiyor. Kriterleri elle girebilirsiniz."));
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(
                ex,
                "AI call prompt={PromptId}@{PromptVersion} latencyMs={LatencyMs} status={Status}",
                PromptId,
                PromptVersion,
                sw.ElapsedMilliseconds,
                "error");
            return Result.Failure<ExtractCriteriaResponse>(
                Error.Unavailable("Servis yanıt vermiyor. Kriterleri elle girebilirsiniz."));
        }
    }

    private static bool IsServiceUnavailable(Exception ex) =>
        ex is HttpRequestException
            or TimeoutException
            or TaskCanceledException
            or InvalidOperationException;

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= 800 ? value : value[..800] + "…";
    }
}
