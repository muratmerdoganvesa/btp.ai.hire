using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
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
/// Calls the hosted SAP orchestration (jd-criteria-extraction-v1). Prompt lives in AI Core;
/// we only send jd_title / jd_text and map the JSON response.
/// </summary>
public sealed class CriteriaExtractionService(
    IAiGateway gateway,
    ILogger<CriteriaExtractionService> logger) : ICriteriaExtractionService
{
    private const string OrchestrationId = "jd-criteria-extraction-v1";
    private const string PromptVersion = "0.0.1";
    private const int MinDescriptionLength = 100;
    private const int MaxDescriptionLength = 20_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
                        ["jd_text"] = description
                    },
                    PlaceholdersOnly: true),
                new AiOptions(MaxOutputTokens: 8000, Temperature: 0),
                cancellationToken);

            sw.Stop();
            var payload = ParsePayload(aiResult.Value) ?? new CriteriaExtractionAiPayload();
            var normalized = Normalize(payload);

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
                Error.Unavailable("Servis yanıt vermiyor. Kriterleri elle girebilirsiniz."));
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
                Error.Unavailable("Servis yanıt vermiyor. Kriterleri elle girebilirsiniz."));
        }
    }

    private static bool IsServiceUnavailable(Exception ex) =>
        ex is HttpRequestException
            or TimeoutException
            or TaskCanceledException
            or InvalidOperationException;

    private static CriteriaExtractionAiPayload? ParsePayload(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

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

        try
        {
            return JsonSerializer.Deserialize<CriteriaExtractionAiPayload>(trimmed, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ExtractCriteriaResponse Normalize(CriteriaExtractionAiPayload payload)
    {
        var rawCriteria = ExtractCriteria(payload);
        var criteria = NormalizeWeights(rawCriteria);

        var flagged = (payload.FlaggedPhrases ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Phrase))
            .Select(f => new FlaggedPhraseDto(
                f.Phrase!.Trim(),
                f.Category?.Trim() ?? string.Empty,
                f.Reason?.Trim() ?? string.Empty))
            .ToList();

        var unmeasurable = (payload.Unmeasurable ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u.Phrase))
            .Select(u => new UnmeasurablePhraseDto(
                u.Phrase!.Trim(),
                u.Reason?.Trim() ?? string.Empty))
            .ToList();

        var interviewQuestions = (payload.InterviewQuestions ?? [])
            .Where(q => !string.IsNullOrWhiteSpace(q.Question))
            .Select(q => new ExtractedInterviewQuestionDto(
                string.IsNullOrWhiteSpace(q.QuestionId) ? string.Empty : q.QuestionId.Trim(),
                string.IsNullOrWhiteSpace(q.CriterionId) ? string.Empty : q.CriterionId.Trim(),
                q.Question!.Trim(),
                (q.WhatToListenFor ?? [])
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Select(h => h.Trim())
                    .ToList()))
            .Take(5)
            .ToList();

        var warnings = (payload.Warnings ?? [])
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new ExtractCriteriaResponse(
            criteria,
            flagged,
            unmeasurable,
            criteria.Sum(c => c.Weight),
            interviewQuestions,
            warnings);
    }

    private static List<ExtractedCriterionDto> ExtractCriteria(CriteriaExtractionAiPayload payload)
    {
        var fromRubric = (payload.Rubric?.Criteria ?? [])
            .Select(MapCriterion)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        if (fromRubric.Count > 0)
        {
            return fromRubric;
        }

        return (payload.Criteria ?? [])
            .Select(MapCriterion)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();
    }

    private static ExtractedCriterionDto? MapCriterion(CriterionAi c)
    {
        var label = FirstNonEmpty(c.Name, c.Label);
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var description = string.IsNullOrWhiteSpace(c.Description) ? label : c.Description.Trim();
        return new ExtractedCriterionDto(
            label.Trim(),
            description,
            Math.Max(0, c.Weight),
            c.Mandatory);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    internal static IReadOnlyList<ExtractedCriterionDto> NormalizeWeights(
        IReadOnlyList<ExtractedCriterionDto> criteria)
    {
        if (criteria.Count == 0)
        {
            return criteria;
        }

        var sum = criteria.Sum(c => c.Weight);
        if (sum == 100)
        {
            return criteria;
        }

        if (sum <= 0)
        {
            var baseWeight = 100 / criteria.Count;
            var remainder = 100 - (baseWeight * criteria.Count);
            return criteria
                .Select((c, i) => c with { Weight = baseWeight + (i < remainder ? 1 : 0) })
                .ToList();
        }

        var scaled = criteria
            .Select(c => c with { Weight = (int)Math.Round(c.Weight * 100.0 / sum, MidpointRounding.AwayFromZero) })
            .ToList();

        var scaledSum = scaled.Sum(c => c.Weight);
        var diff = 100 - scaledSum;
        if (diff != 0)
        {
            var index = scaled
                .Select((c, i) => (c.Weight, i))
                .OrderByDescending(x => x.Weight)
                .ThenBy(x => x.i)
                .First()
                .i;
            scaled[index] = scaled[index] with { Weight = Math.Max(1, scaled[index].Weight + diff) };
        }

        return scaled;
    }

    private sealed class CriteriaExtractionAiPayload
    {
        public RubricAi? Rubric { get; set; }

        public List<CriterionAi>? Criteria { get; set; }

        public List<InterviewQuestionAi>? InterviewQuestions { get; set; }

        public List<string>? Warnings { get; set; }

        public List<FlaggedAi>? FlaggedPhrases { get; set; }

        public List<UnmeasurableAi>? Unmeasurable { get; set; }

        public int? TotalWeight { get; set; }
    }

    private sealed class RubricAi
    {
        public List<CriterionAi>? Criteria { get; set; }

        public int? WeightTotal { get; set; }
    }

    private sealed class CriterionAi
    {
        public string? CriterionId { get; set; }

        public string? Name { get; set; }

        public string? Label { get; set; }

        public string? Description { get; set; }

        public int Weight { get; set; }

        public bool Mandatory { get; set; }
    }

    private sealed class InterviewQuestionAi
    {
        public string? QuestionId { get; set; }

        public string? CriterionId { get; set; }

        public string? Question { get; set; }

        public List<string>? WhatToListenFor { get; set; }
    }

    private sealed class FlaggedAi
    {
        public string? Phrase { get; set; }

        public string? Category { get; set; }

        public string? Reason { get; set; }
    }

    private sealed class UnmeasurableAi
    {
        public string? Phrase { get; set; }

        public string? Reason { get; set; }
    }
}
