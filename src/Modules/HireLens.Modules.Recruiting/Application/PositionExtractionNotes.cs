using System.Text.Json;
using HireLens.Contracts.Recruiting;

namespace HireLens.Modules.Recruiting.Application;

public static class PositionExtractionNotes
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(
        IReadOnlyList<UnmeasurablePhraseDto>? unmeasurable,
        IReadOnlyList<FlaggedPhraseDto>? flaggedPhrases)
    {
        var payload = new Notes(
            NormalizeUnmeasurable(unmeasurable),
            NormalizeFlagged(flaggedPhrases));
        if (payload.Unmeasurable.Count == 0 && payload.FlaggedPhrases.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(payload, Json);
    }

    public static Notes Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() is "{}" or "[]")
        {
            return Notes.Empty;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Notes>(json, Json);
            return parsed is null
                ? Notes.Empty
                : new Notes(NormalizeUnmeasurable(parsed.Unmeasurable), NormalizeFlagged(parsed.FlaggedPhrases));
        }
        catch (JsonException)
        {
            return Notes.Empty;
        }
    }

    private static IReadOnlyList<UnmeasurablePhraseDto> NormalizeUnmeasurable(
        IReadOnlyList<UnmeasurablePhraseDto>? items) =>
        (items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Phrase))
            .Select(i => new UnmeasurablePhraseDto(i.Phrase.Trim(), i.Reason?.Trim() ?? string.Empty))
            .ToList();

    private static IReadOnlyList<FlaggedPhraseDto> NormalizeFlagged(IReadOnlyList<FlaggedPhraseDto>? items) =>
        (items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Phrase))
            .Select(i => new FlaggedPhraseDto(
                i.Phrase.Trim(),
                i.Category?.Trim() ?? string.Empty,
                i.Reason?.Trim() ?? string.Empty))
            .ToList();

    public sealed record Notes(
        IReadOnlyList<UnmeasurablePhraseDto> Unmeasurable,
        IReadOnlyList<FlaggedPhraseDto> FlaggedPhrases)
    {
        public static Notes Empty { get; } = new([], []);
    }
}
