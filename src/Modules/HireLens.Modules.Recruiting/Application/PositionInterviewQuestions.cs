using System.Text.Json;
using HireLens.Contracts.Recruiting;

namespace HireLens.Modules.Recruiting.Application;

public static class PositionInterviewQuestions
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(IReadOnlyList<ExtractedInterviewQuestionDto>? questions)
    {
        var items = Normalize(questions);
        return items.Count == 0 ? "[]" : JsonSerializer.Serialize(items, Json);
    }

    public static IReadOnlyList<ExtractedInterviewQuestionDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
        {
            return [];
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<List<ExtractedInterviewQuestionDto>>(json, Json));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<ExtractedInterviewQuestionDto> Normalize(
        IReadOnlyList<ExtractedInterviewQuestionDto>? questions) =>
        (questions ?? [])
            .Where(q => !string.IsNullOrWhiteSpace(q.Question))
            .Select(q => new ExtractedInterviewQuestionDto(
                string.IsNullOrWhiteSpace(q.QuestionId) ? string.Empty : q.QuestionId.Trim(),
                string.IsNullOrWhiteSpace(q.CriterionId) ? string.Empty : q.CriterionId.Trim(),
                q.Question.Trim(),
                (q.WhatToListenFor ?? [])
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Select(h => h.Trim())
                    .ToList()))
            .Take(5)
            .ToList();
}
