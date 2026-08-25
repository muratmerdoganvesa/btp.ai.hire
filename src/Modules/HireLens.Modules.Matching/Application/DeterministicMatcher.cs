using HireLens.Contracts.Evidence;
using HireLens.Contracts.Recruiting;

namespace HireLens.Modules.Matching.Application;

/// <summary>
/// Stable, evidence-bound matcher used when Orchestration is absent and as
/// a schema fallback. Same CV+JD always yields the same scores (variance 0).
/// </summary>
public static class DeterministicMatcher
{
    public static IReadOnlyList<ProposedCriterionScore> Score(string maskedCv, PositionSnapshot position)
    {
        var proposals = new List<ProposedCriterionScore>();
        foreach (var criterion in position.Criteria)
        {
            var needle = criterion.Name.Trim();
            var index = maskedCv.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                foreach (var token in needle.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    index = maskedCv.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        needle = token;
                        break;
                    }
                }
            }

            if (index < 0)
            {
                proposals.Add(new ProposedCriterionScore(criterion.Id, criterion.Weight, null, 0.2, []));
                continue;
            }

            var end = Math.Min(maskedCv.Length, index + Math.Max(needle.Length, 24));
            var quote = maskedCv[index..end].Trim();
            proposals.Add(new ProposedCriterionScore(
                criterion.Id,
                criterion.Weight,
                78,
                0.8,
                [new ProposedEvidence("cv", quote, index, end)]));
        }

        return proposals;
    }

    public static int? Overall(IReadOnlyList<ProposedCriterionScore> scores) =>
        ToScoreResult(scores, "deterministic-v1").Total is { } total
            ? (int)Math.Round(total)
            : null;

    public static ScoreResult ToScoreResult(
        IReadOnlyList<ProposedCriterionScore> scores,
        string rubricVersion)
    {
        var rubric = Rubric.FromWeights(
            rubricVersion,
            scores.Select(s => (s.CriterionId.ToString(), (decimal)s.Weight)));

        var match = new CriteriaMatch(
            scores.Select(s => new CriterionMatchResult(
                s.CriterionId.ToString(),
                s.Score.HasValue ? s.Score.Value : null,
                s.Score is null ? MatchConfidence.None : ScoreCalculator.MapConfidence(s.Confidence)))
            .ToList());

        return ScoreCalculator.Calculate(match, rubric);
    }
}
