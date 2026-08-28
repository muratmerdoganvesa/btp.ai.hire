using HireLens.Contracts.Evidence;

namespace HireLens.Modules.Matching.Application;

/// <summary>
/// Recruiter-facing CV summary from scored criteria. Used when RecruiterSummary
/// AI returns empty or a generic placeholder.
/// </summary>
public static class EvaluationNarrative
{
    public static bool IsGenericPlaceholder(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return text.Contains("Evidence-bound scores are ready", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Insufficient evidence for an overall score", StringComparison.OrdinalIgnoreCase);
    }

    public static string Build(
        int? overall,
        decimal coverage,
        IReadOnlyList<ProposedCriterionScore> proposals,
        IReadOnlyDictionary<Guid, string> names)
    {
        var coveragePct = (int)Math.Round(coverage * 100);
        var scored = proposals
            .Where(p => p.Score is not null)
            .OrderByDescending(p => p.Score)
            .ToList();
        var missing = proposals
            .Where(p => p.Score is null)
            .Select(p => NameOf(p.CriterionId, names))
            .Take(6)
            .ToList();
        var strengths = scored
            .Take(3)
            .Select(p => NameOf(p.CriterionId, names))
            .ToList();

        if (overall is null)
        {
            var gaps = missing.Count == 0
                ? "kriterlerin çoğunda alıntı yok"
                : string.Join(", ", missing);
            return $"Sayısal skor yazılamadı. Kapsam %{coveragePct}. Kanıt bulunamayan: {gaps}.";
        }

        var strong = strengths.Count == 0
            ? "belirgin bir güçlü alan yok"
            : string.Join(", ", strengths);
        if (missing.Count == 0)
        {
            return $"Skor {overall} / 100, kapsam %{coveragePct}. CV’de öne çıkan: {strong}.";
        }

        return $"Skor {overall} / 100, kapsam %{coveragePct}. CV’de öne çıkan: {strong}. Kanıt bulunamayan: {string.Join(", ", missing)}.";
    }

    private static string NameOf(Guid id, IReadOnlyDictionary<Guid, string> names) =>
        names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : "kriter";
}
