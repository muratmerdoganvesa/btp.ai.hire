using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
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

public sealed class CriteriaExtractionService(
    IAiGateway gateway,
    IPromptRegistry prompts,
    ILogger<CriteriaExtractionService> logger) : ICriteriaExtractionService
{
    private const string PromptId = "CriteriaExtraction";
    private const string PromptVersion = "1";
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

        PromptDefinition prompt;
        try
        {
            prompt = prompts.Get(PromptId, PromptVersion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CriteriaExtraction prompt missing");
            return Result.Failure<ExtractCriteriaResponse>(
                Error.Unavailable("Servis yanıt vermiyor. Kriterleri elle girebilirsiniz."));
        }

        var sw = Stopwatch.StartNew();
        try
        {
            // Deserialize in this assembly: AiGateway cannot construct private payload types.
            var aiResult = await gateway.ExecuteAsync<string>(
                AiTaskType.CriteriaExtraction,
                new PromptContext(
                    TaskInput: $"{title}\n---\n{description}",
                    PromptVersion: prompt.Version,
                    Variables: new Dictionary<string, string>
                    {
                        ["job_title"] = title,
                        ["job_description"] = description
                    },
                    SystemPrompt: prompt.SystemPrompt,
                    UserPrompt: prompt.UserTemplate),
                new AiOptions(MaxOutputTokens: 4000, Temperature: 0),
                cancellationToken);

            sw.Stop();
            var payload = ParsePayload(aiResult.Value) ?? new CriteriaExtractionAiPayload();
            var normalized = Normalize(payload);

            logger.LogInformation(
                "AI call promptId={PromptId} promptVersion={PromptVersion} model={Model} inputTokens={InputTokens} outputTokens={OutputTokens} latencyMs={LatencyMs} status={Status} criteriaCount={CriteriaCount} warnings={Warnings}",
                PromptId,
                prompt.Version,
                aiResult.ModelId,
                aiResult.InputTokens,
                aiResult.OutputTokens,
                (long)aiResult.Latency.TotalMilliseconds,
                "ok",
                normalized.Criteria.Count,
                string.Join(',', aiResult.Warnings));

            return Result.Success(normalized);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            sw.Stop();
            logger.LogWarning(
                ex,
                "AI call promptId={PromptId} promptVersion={PromptVersion} latencyMs={LatencyMs} status={Status}",
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
                "AI call promptId={PromptId} promptVersion={PromptVersion} latencyMs={LatencyMs} status={Status}",
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
        var raw = (payload.Criteria ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Label))
            .Select(c => new ExtractedCriterionDto(
                c.Label!.Trim(),
                string.IsNullOrWhiteSpace(c.Description) ? c.Label!.Trim() : c.Description.Trim(),
                Math.Max(0, c.Weight),
                c.Mandatory))
            .ToList();

        var criteria = NormalizeWeights(raw);

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

        return new ExtractCriteriaResponse(
            criteria,
            flagged,
            unmeasurable,
            criteria.Sum(c => c.Weight));
    }

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
        public List<CriterionAi>? Criteria { get; set; }

        public List<FlaggedAi>? FlaggedPhrases { get; set; }

        public List<UnmeasurableAi>? Unmeasurable { get; set; }

        public int? TotalWeight { get; set; }
    }

    private sealed class CriterionAi
    {
        public string? Label { get; set; }

        public string? Description { get; set; }

        public int Weight { get; set; }

        public bool Mandatory { get; set; }
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
